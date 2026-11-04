import { enabledOrigins, pageOrigin, permissionFor, siteKey } from "./sites.js";

const message = key => chrome.i18n.getMessage(key);
const status = document.querySelector("#status");
const toggle = document.querySelector("#toggle");
const done = document.querySelector("#done");
document.documentElement.lang = chrome.i18n.getUILanguage();
document.querySelector("#privacy").textContent = message("privacy");
done.textContent = message("done");
let sourceTabId;
let origin;
let enabled;

async function sourceMatches() {
  try {
    const [self, source] = await Promise.all([chrome.tabs.getCurrent(), chrome.tabs.get(sourceTabId)]);
    return self?.openerTabId === sourceTabId && self.windowId === source.windowId &&
      !source.pendingUrl && pageOrigin(source.url) === origin;
  } catch { return false; }
}

function sourceChanged() {
  toggle.disabled = true;
  status.textContent = message("sourceChanged");
}

async function removeUnusedPermission(perm) {
  const siblings = await enabledOrigins();
  if (!siblings.some(site => permissionFor(site).origins[0] === perm.origins[0])) {
    await chrome.permissions.remove(perm);
  }
}

function display() {
  status.textContent = message(enabled ? "enabled" : "disabled");
  toggle.textContent = message(enabled ? "disable" : "enable");
  toggle.disabled = false;
}

toggle.addEventListener("click", async () => {
  toggle.disabled = true;
  try {
    const perm = permissionFor(origin);
    if (enabled) {
      if (!await sourceMatches()) { sourceChanged(); return; }
      await chrome.storage.local.remove(siteKey(origin));
      // Several explicitly enabled sites may share the same host pattern.
      await removeUnusedPermission(perm);
    } else {
      // The stable tab survives Chrome's permission prompt. The request must
      // be the first asynchronous action in this explicit button gesture.
      if (!await chrome.permissions.request(perm)) {
        display();
        status.textContent = message("denied");
        return;
      }
      if (!await sourceMatches() || !await chrome.permissions.contains(perm)) {
        await removeUnusedPermission(perm);
        sourceChanged();
        return;
      }
      await chrome.storage.local.set({ [siteKey(origin)]: true });
    }
    enabled = (await enabledOrigins()).includes(origin);
    display();
  } catch {
    status.textContent = message("failure");
  }
});

done.addEventListener("click", async () => {
  const self = await chrome.tabs.getCurrent();
  if (!self) return;
  try {
    if (self.openerTabId !== undefined) {
      const source = await chrome.tabs.get(self.openerTabId);
      if (source.windowId === self.windowId) await chrome.tabs.update(source.id, { active: true });
    }
  } catch { /* Source tab closed. */ }
  await chrome.tabs.remove(self.id);
});

chrome.tabs.onRemoved.addListener(tabId => { if (tabId === sourceTabId) sourceChanged(); });
chrome.tabs.onUpdated.addListener(async (tabId, change) => {
  if (tabId === sourceTabId && (change.status || "url" in change) && !await sourceMatches()) sourceChanged();
});

try {
  const self = await chrome.tabs.getCurrent();
  sourceTabId = self?.openerTabId;
  const source = sourceTabId === undefined ? null : await chrome.tabs.get(sourceTabId);
  origin = pageOrigin(source?.url);
  if (origin && await sourceMatches()) {
    document.querySelector("#site").textContent = origin;
    enabled = (await enabledOrigins()).includes(origin);
    display();
  } else {
    toggle.hidden = true;
    status.textContent = message("unsupported");
  }
} catch {
  toggle.hidden = true;
  status.textContent = message("failure");
}
