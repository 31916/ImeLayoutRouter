import { enabledOrigins, pageOrigin, permissionFor, siteKey } from "./sites.js";
import { inspectField } from "./probe.js";

const host = "ch.imelayoutrouter.numeric";
const retryAlarm = "retry-native";
let port;
let retryAfter = 0;
let revision = 0;
let hostRevision = 0;

async function activePage(sites) {
  if (!sites.length) return null;
  const win = await chrome.windows.getLastFocused({ windowTypes: ["normal"] });
  if (!win.focused || win.incognito) return null;
  // Query only hosts the user has enrolled. Never inspect a title or page content.
  const urls = [...new Set(sites.map(site => permissionFor(site).origins[0]))];
  const [tab] = await chrome.tabs.query({ active: true, windowId: win.id, url: urls });
  const origin = pageOrigin(tab?.url);
  if (!tab || tab.incognito || tab.pendingUrl || !sites.includes(origin)) return null;
  return { tabId: tab.id, windowId: win.id, origin };
}

async function classify() {
  const start = revision;
  const sites = await enabledOrigins();
  const page = await activePage(sites);
  if (!page || start !== revision) return "unknown";

  const inspect = options => chrome.scripting.executeScript({
    target: { tabId: page.tabId, ...options },
    world: "ISOLATED", func: inspectField, args: [sites, page.origin]
  });
  let [candidate] = await inspect({ frameIds: [0] });
  if (candidate?.result === "child") {
    // Some browsers reject all-frame injection if any frame is unauthorized.
    // In that case the entire query fails closed. The probe checks the exact
    // permissions of the frame and every embedding origin before inspecting it.
    candidate = (await inspect({ allFrames: true })).find(entry => entry.result === "numeric");
  }
  if (candidate?.result !== "numeric" || !candidate.documentId || start !== revision) return "unknown";
  // Verify that the same document still has a focused numeric field after
  // the asynchronous frame lookup. A navigation invalidates its documentId.
  const [verified] = await inspect({ documentIds: [candidate.documentId] });
  if (verified?.result !== "numeric") return "unknown";
  const current = await activePage(sites);
  return start === revision && current?.windowId === page.windowId &&
    current.tabId === page.tabId && current.origin === page.origin ? "numeric" : "unknown";
}

function attach(connection) {
  let busy = false;
  connection.onMessage.addListener(async message => {
    if (message?.op !== "classify" || !Number.isSafeInteger(message.id) || message.id <= 0) return;
    let kind = "unknown";
    if (!busy) {
      busy = true;
      try { kind = await classify(); }
      catch { /* Gone, restricted, or no longer permitted. */ }
      finally { busy = false; }
    }
    if (port === connection) {
      try { connection.postMessage({ id: message.id, kind }); } catch { /* Disconnected. */ }
    }
  });
  connection.onDisconnect.addListener(() => {
    // Chrome sets lastError when a host is absent. Never log host messages.
    void chrome.runtime.lastError;
    if (port !== connection) return;
    port = undefined;
    retryAfter = Date.now() + 60000;
    void reconcile();
  });
}

async function reconcile() {
  const request = ++hostRevision;
  const sites = await enabledOrigins();
  if (request !== hostRevision) return;
  if (!sites.length) {
    const previous = port;
    port = undefined;
    previous?.disconnect();
    retryAfter = 0;
    await chrome.alarms.clear(retryAlarm);
  } else if (!port) {
    if (Date.now() < retryAfter) {
      chrome.alarms.create(retryAlarm, { when: retryAfter });
      return;
    }
    port = chrome.runtime.connectNative(host);
    attach(port);
  }
}

async function permissionsRemoved() {
  const entries = await chrome.storage.local.get(null);
  const enabled = new Set((await enabledOrigins()).map(siteKey));
  const stale = Object.keys(entries).filter(key => key.startsWith("site:") && !enabled.has(key));
  if (stale.length) await chrome.storage.local.remove(stale);
  await reconcile();
}

chrome.storage.onChanged.addListener((_, area) => {
  if (area === "local") { revision++; void reconcile(); }
});
chrome.permissions.onRemoved.addListener(() => { revision++; void permissionsRemoved(); });
chrome.alarms.onAlarm.addListener(alarm => { if (alarm.name === retryAlarm) void reconcile(); });
chrome.tabs.onActivated.addListener(() => { revision++; });
chrome.tabs.onUpdated.addListener((_, change) => { if (change.status === "loading") revision++; });
chrome.windows.onFocusChanged.addListener(() => { revision++; });
chrome.runtime.onStartup.addListener(() => { void reconcile(); });
chrome.runtime.onInstalled.addListener(() => { void reconcile(); });
void reconcile();
