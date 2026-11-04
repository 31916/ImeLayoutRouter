import { enabledOrigins, pageOrigin, permissionFor, siteKey } from "./sites.js";
import { inspectField } from "./probe.js";

const host = "ch.imelayoutrouter.numeric";
const retryAlarm = "retry-native";
let port;
let connectedPage;
let windowBlurred = false;
let retryAfter = 0;
let revision = 0;
let hostRevision = 0;

async function activePage(sites) {
  if (!sites.length || windowBlurred) return null;
  const win = await chrome.windows.getLastFocused({ windowTypes: ["normal"] });
  if (!win.focused || win.incognito || win.state === "minimized") return null;
  // Query only hosts the user has enrolled. Never inspect a title or page content.
  const urls = [...new Set(sites.map(site => permissionFor(site).origins[0]))];
  const [tab] = await chrome.tabs.query({ active: true, windowId: win.id, url: urls });
  const origin = pageOrigin(tab?.url);
  if (!tab || tab.incognito || tab.discarded || tab.pendingUrl || tab.status === "loading" || !sites.includes(origin)) return null;
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
  // A permission change can precede its event. Recheck grants, including
  // subframe origins, before accepting the result of either injected probe.
  const currentSites = await enabledOrigins();
  if (!sites.every(site => currentSites.includes(site))) return "unknown";
  const current = await activePage(currentSites);
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
    connectedPage = undefined;
    retryAfter = Date.now() + 60000;
    void reconcile();
  });
}

function disconnect() {
  const previous = port;
  port = undefined;
  connectedPage = undefined;
  previous?.disconnect();
}

async function reconcile() {
  const request = ++hostRevision;
  let page = null;
  try { page = await activePage(await enabledOrigins()); }
  catch { /* Closing the last window can invalidate an in-flight query. */ }
  if (request !== hostRevision) return;
  if (!page) {
    disconnect();
    await chrome.alarms.clear(retryAlarm);
    return;
  }
  if (port && connectedPage?.tabId === page.tabId && connectedPage.windowId === page.windowId &&
      connectedPage.origin === page.origin) return;
  disconnect();
  if (Date.now() < retryAfter) {
    chrome.alarms.create(retryAlarm, { when: retryAfter });
    return;
  }
  // A suspended worker loses its JS timer state; Chrome retains the alarm.
  const retry = await chrome.alarms.get(retryAlarm);
  if (request !== hostRevision || retry?.scheduledTime > Date.now()) return;
  try {
    port = chrome.runtime.connectNative(host);
    connectedPage = page;
    attach(port);
  } catch {
    disconnect();
    retryAfter = Date.now() + 60000;
    chrome.alarms.create(retryAlarm, { when: retryAfter });
  }
}

function changed() {
  revision++;
  return reconcile();
}

async function permissionsRemoved() {
  disconnect();
  revision++;
  hostRevision++;
  const entries = await chrome.storage.local.get(null);
  const enabled = new Set((await enabledOrigins()).map(siteKey));
  const stale = Object.keys(entries).filter(key => key.startsWith("site:") && !enabled.has(key));
  if (stale.length) await chrome.storage.local.remove(stale);
  await reconcile();
}

chrome.storage.onChanged.addListener((_, area) => {
  if (area === "local") return changed();
});
chrome.permissions.onAdded.addListener(changed);
chrome.permissions.onRemoved.addListener(permissionsRemoved);
chrome.alarms.onAlarm.addListener(alarm => { if (alarm.name === retryAlarm) void reconcile(); });
chrome.tabs.onActivated.addListener(info => {
  if (connectedPage && info.windowId === connectedPage.windowId && info.tabId !== connectedPage.tabId) disconnect();
  return changed();
});
chrome.tabs.onUpdated.addListener((tabId, update, tab) => {
  if (!tab.active || (!update.status && !("url" in update))) return;
  if (connectedPage?.tabId === tabId && (update.status === "loading" || "url" in update)) disconnect();
  return changed();
});
chrome.tabs.onRemoved.addListener(tabId => {
  if (!connectedPage || connectedPage.tabId === tabId) {
    disconnect();
    return changed();
  }
});
chrome.windows.onFocusChanged.addListener(windowId => {
  windowBlurred = windowId === chrome.windows.WINDOW_ID_NONE;
  if (connectedPage && connectedPage.windowId !== windowId) disconnect();
  return changed();
});
chrome.runtime.onStartup.addListener(() => { void reconcile(); });
chrome.runtime.onInstalled.addListener(() => { void reconcile(); });
chrome.action.onClicked.addListener(tab => {
  if (!Number.isInteger(tab.id) || tab.incognito) return;
  // Preserve Chrome's explicit activeTab grant for the clicked site. The
  // stable extension tab receives no URL, title or origin in its own address.
  return chrome.tabs.create({
    url: chrome.runtime.getURL("setup.html"), openerTabId: tab.id,
    windowId: tab.windowId, index: tab.index + 1, active: true
  }).catch(() => { /* The originating window closed. */ });
});
void reconcile();
