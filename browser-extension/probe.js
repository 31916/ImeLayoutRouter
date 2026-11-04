// Chrome serializes this function into an isolated world. Keep it self-contained.
export function inspectField(allowed, topOrigin) {
  function numericPattern(pattern) {
    // HTML implicitly anchors patterns. Recognize only a single ASCII digit class
    // with an optional count. Never execute the site's expression.
    if (!pattern || pattern.length > 64) return false;
    const match = /^\^?(?:\[0-9\]|\\d)(?:[+*?]|\{(\d{1,6})(?:(,)(\d{0,6}))?\})?\$?$/.exec(pattern);
    if (!match) return false;
    if (!match[1]) return true;
    if (!match[2]) return Number(match[1]) > 0;
    return match[3] === "" || (Number(match[3]) > 0 && Number(match[3]) >= Number(match[1]));
  }

  const here = window.location.origin;
  if (!allowed.includes(here)) return "unknown";
  const ancestors = Array.from(window.location.ancestorOrigins);
  if (window === window.top ? here !== topOrigin :
      ancestors.at(-1) !== topOrigin || !ancestors.every(origin => allowed.includes(origin))) return "unknown";

  let doc = document;
  for (let depth = 0; depth < 32 && doc.hasFocus(); depth++) {
    let field = doc.activeElement;
    while (field?.shadowRoot?.activeElement) field = field.shadowRoot.activeElement;
    if (!field || field.namespaceURI !== "http://www.w3.org/1999/xhtml") return "unknown";
    if (field.localName === "iframe" || field.localName === "frame") {
      const nested = field.contentDocument;
      if (!nested) return "child";
      if (!allowed.includes(nested.location.origin)) return "unknown";
      doc = nested;
      continue;
    }
    const input = field.localName === "input";
    if (!input && field.localName !== "textarea") return "unknown";
    if (field.readOnly || field.matches(":disabled") || field.closest("[inert]")) return "unknown";
    if (input && field.type === "number") return "numeric";
    if (input && !["text", "tel", "search"].includes(field.type)) return "unknown";
    if (field.inputMode === "numeric" || field.inputMode === "decimal") return "numeric";
    return input && numericPattern(field.getAttribute("pattern")) ? "numeric" : "unknown";
  }
  return "unknown";
}
