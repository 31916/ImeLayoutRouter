export const siteKey = origin => "site:" + origin;

export function pageOrigin(url) {
  try {
    const parsed = new URL(url);
    if (parsed.protocol === "http:" || parsed.protocol === "https:") return parsed.origin;
  } catch { /* The browser may omit a URL the extension cannot access. */ }
  return null;
}

// Chrome grants host patterns across ports. The additional enrollment key
// is an exact origin, so a permit for one port does not enroll its neighbors.
export function permissionFor(origin) {
  const url = new URL(origin);
  return { origins: [url.protocol + "//" + url.hostname + "/*"] };
}

export async function enabledOrigins() {
  const entries = await chrome.storage.local.get(null);
  const sites = Object.keys(entries).filter(key => entries[key] === true && key.startsWith("site:"))
    .map(key => key.slice(5)).filter(origin => pageOrigin(origin) === origin);
  const checks = await Promise.all(sites.map(origin => chrome.permissions.contains(permissionFor(origin))));
  return sites.filter((_, i) => checks[i]);
}
