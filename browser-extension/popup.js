import { enabledOrigins, pageOrigin, permissionFor, siteKey } from "./sites.js";

const message = key => chrome.i18n.getMessage(key);
const status = document.querySelector("#status");
const toggle = document.querySelector("#toggle");
document.documentElement.lang = chrome.i18n.getUILanguage();
document.querySelector("#privacy").textContent = message("privacy");
let origin;
let enabled;

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
      await chrome.storage.local.remove(siteKey(origin));
      // Several explicitly enabled sites may share the same host pattern.
      const siblings = await enabledOrigins();
      if (!siblings.some(site => permissionFor(site).origins[0] === perm.origins[0])) {
        await chrome.permissions.remove(perm);
      }
    } else {
      // Request directly in the user gesture, with only this host's pattern.
      if (!await chrome.permissions.request(perm)) {
        display();
        status.textContent = message("denied");
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

try {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  origin = pageOrigin(tab?.url);
  if (origin) {
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
