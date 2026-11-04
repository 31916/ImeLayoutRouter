import test from "node:test";
import assert from "node:assert/strict";
import { inspectField } from "../browser-extension/probe.js";

const topSite = "https://fixture.example";
const childSite = "https://input.example";
const approved = [topSite, childSite];

function input(options = {}) {
  const { pattern, ...props } = options;
  return {
    localName: "input", namespaceURI: "http://www.w3.org/1999/xhtml",
    type: "text", inputMode: "", readOnly: false, matches: () => false, closest: () => null,
    getAttribute(name) { assert.equal(name, "pattern"); return pattern ?? null; },
    get value() { throw Error("The probe read entered digits"); },
    get textContent() { throw Error("The probe read page text"); },
    get innerHTML() { throw Error("The probe read markup"); },
    ...props
  };
}

function fieldInPage(field, { origin = topSite, ancestors = [], focused = true, hidden = false } = {}) {
  globalThis.window = { location: { origin, ancestorOrigins: ancestors } };
  window.top = ancestors.length ? {} : window;
  globalThis.document = { hasFocus: () => focused, hidden, activeElement: field };
  return inspectField(approved, topSite);
}

test("numeric input hints and bounded patterns, without reading the value", () => {
  for (const p of [{type: "number"}, {inputMode: "numeric"}, {inputMode: "decimal"},
      ...["[0-9]+", "\\d{6}", "^[0-9]{1,6}$", "\\d{2,}"].map(pattern => ({pattern}))]) {
    assert.equal(fieldInPage(input(p)), "numeric");
  }
  for (const p of [{readOnly: true, inputMode: "numeric"}, {matches: () => true, inputMode: "numeric"},
      {type: "range", inputMode: "numeric"}, {type: "password", inputMode: "numeric"}, {},
      ...["[0-9]*[a-z]*", "123456", "[0-9]{6,2}", "\\d{0}", "\\d{0,0}"].map(pattern => ({pattern}))]) {
    assert.equal(fieldInPage(input(p)), "unknown");
  }
});

test("only a focused, enrolled document and its authorized embedding chain count", () => {
  const field = input({ inputMode: "numeric" });
  assert.equal(fieldInPage(field, {focused: false}), "unknown");
  assert.equal(fieldInPage(field, {hidden: true}), "unknown");
  assert.equal(fieldInPage(field, {origin: "https://unapproved.example"}), "unknown");
  assert.equal(fieldInPage(field, {origin: childSite, ancestors: [topSite]}), "numeric");
  assert.equal(fieldInPage(field, {origin: childSite, ancestors: ["https://elsewhere.example", topSite]}), "unknown");
  assert.equal(fieldInPage(field, {origin: childSite, ancestors: ["https://elsewhere.example"]}), "unknown");
  const host = input({localName: "custom-field", shadowRoot: {activeElement: field}});
  assert.equal(fieldInPage(host), "numeric");
  const nested = {hasFocus: () => true, activeElement: field, location: {origin: topSite}};
  assert.equal(fieldInPage(input({localName: "iframe", contentDocument: nested})), "numeric");
  assert.equal(fieldInPage(input({localName: "iframe", contentDocument: null})), "child");
});

function event() {
  const listeners = [];
  return {
    addListener(fn) { listeners.push(fn); },
    fire(...args) { return Promise.all(listeners.map(fn => fn(...args))); }
  };
}

test("the native reply carries only a nonce and classification; stale focus and grants fail closed", async () => {
  let signalReady;
  const ready = new Promise(resolve => { signalReady = resolve; });
  const replies = [];
  let tab = 11, selected = topSite, focused = true, granted = true;
  let sample = () => "numeric";
  let inspections = 0;
  const port = { onMessage: event(), onDisconnect: event(), disconnect() {},
    postMessage(message) { replies.push(message); } };
  const entries = { ["site:"+topSite]: true };
  globalThis.chrome = {
    runtime: { connectNative(host) { assert.equal(host, "ch.imelayoutrouter.numeric"); signalReady(); return port; },
      onStartup: event(), onInstalled: event() },
    storage: { local: { get: async () => entries, remove: async () => {} }, onChanged: event() },
    permissions: { contains: async () => granted, onRemoved: event(), onAdded: event() },
    windows: { getLastFocused: async () => ({ id: 1, focused }), WINDOW_ID_NONE: -1, onFocusChanged: event() },
    tabs: { query: async () => [{ id: tab, url: selected+"/secret-path?demo=123456" }],
      onActivated: event(), onUpdated: event(), onRemoved: event() },
    scripting: { executeScript: async ({target, world}) => {
      assert.equal(world, "ISOLATED");
      inspections++;
      return [{documentId: "fixture-doc", result: sample(target)}];
    }},
    alarms: { onAlarm: event(), create() {}, get: async () => undefined, clear: async () => true }
  };
  await import("../browser-extension/worker.js");
  await ready;
  let id = 0;
  async function check(kind) {
    await port.onMessage.fire({ op: "classify", id: ++id });
    assert.deepEqual(replies.pop(), { id, kind });
  }
  await check("numeric");
  const before = inspections;
  focused = false;
  await check("unknown");
  focused = true;
  selected = "https://unapproved.example";
  await check("unknown");
  assert.equal(inspections, before);
  selected = topSite;
  sample = target => { if (target.documentIds) tab++; return "numeric"; };
  await check("unknown");
  tab = 11;
  sample = target => target.documentIds ? "unknown" : "numeric";
  await check("unknown");
  sample = () => { chrome.tabs.onUpdated.fire(tab, {status: "complete"}, {active: true}); return "numeric"; };
  await check("unknown");
  sample = target => { if (target.documentIds) granted = false; return "numeric"; };
  await check("unknown");
  granted = false;
  await check("unknown");
  await port.onMessage.fire({ op: "other", id: 1 });
  assert.deepEqual(replies, []);
});

test("the host runs only for an eligible foreground tab; leaving also cancels its retry", async () => {
  let origin = "https://not-enabled.example";
  let tabId = 20, loading = false, grant = true;
  let queried;
  const queriedInitially = new Promise(resolve => { queried = resolve; });
  const ports = [];
  const entries = { ["site:"+topSite]: true };
  let alarm;
  const tick = () => new Promise(resolve => setImmediate(resolve));
  globalThis.chrome = {
    runtime: { connectNative() {
      const port = { closed: false, onMessage: event(), onDisconnect: event(),
        disconnect() { this.closed = true; } };
      ports.push(port);
      return port;
    }, onStartup: event(), onInstalled: event() },
    storage: { local: { get: async () => entries, remove: async keys => { for (const key of keys) delete entries[key]; } }, onChanged: event() },
    permissions: { contains: async () => grant, onAdded: event(), onRemoved: event() },
    windows: { WINDOW_ID_NONE: -1, getLastFocused: async () => ({id: 1, focused: true}), onFocusChanged: event() },
    tabs: { query: async () => { queried(); return [{ id: tabId, url: origin+"/", status: loading ? "loading" : "complete" }]; },
      onActivated: event(), onUpdated: event(), onRemoved: event() },
    alarms: { onAlarm: event(), create: (_, opts) => { alarm = {scheduledTime: opts.when}; },
      get: async () => alarm, clear: async () => { alarm = undefined; } }
  };
  await import("../browser-extension/worker.js?port-lifetime");
  await queriedInitially;
  await tick();
  assert.equal(ports.length, 0);
  origin = topSite;
  await chrome.tabs.onActivated.fire({windowId: 1, tabId: ++tabId});
  assert.equal(ports.length, 1);
  origin = "https://not-enabled.example";
  const activating = chrome.tabs.onActivated.fire({windowId: 1, tabId: ++tabId});
  assert.equal(ports[0].closed, true, "revoked immediately on active tab change");
  await activating;
  assert.equal(ports.length, 1);
  origin = topSite;
  await chrome.tabs.onUpdated.fire(tabId, {status: "complete"}, {active: true});
  assert.equal(ports.length, 2);
  loading = true;
  await chrome.tabs.onUpdated.fire(tabId, {status: "loading"}, {active: true});
  assert.equal(ports[1].closed, true);
  assert.equal(ports.length, 2);
  loading = false;
  await chrome.tabs.onUpdated.fire(tabId, {status: "complete"}, {active: true});
  assert.equal(ports.length, 3);
  const blurring = chrome.windows.onFocusChanged.fire(-1);
  assert.equal(ports[2].closed, true, "revoked immediately on losing browser focus");
  await blurring;
  await chrome.runtime.onStartup.fire();
  await tick();
  assert.equal(ports.length, 3, "a stale getLastFocused result cannot undo the blur event");
  await chrome.windows.onFocusChanged.fire(1);
  assert.equal(ports.length, 4);
  grant = false;
  await chrome.permissions.onRemoved.fire();
  assert.equal(ports[3].closed, true);
  assert.equal(ports.length, 4);
  grant = true;
  await chrome.permissions.onAdded.fire();
  assert.equal(ports.length, 4, "restoring a host permission alone does not reenroll the site");
  entries["site:"+topSite] = true;
  await chrome.storage.onChanged.fire({}, "local");
  assert.equal(ports.length, 5);
  await ports[4].onDisconnect.fire();
  await tick();
  assert.ok(alarm?.scheduledTime > Date.now());
  origin = "https://not-enabled.example";
  await chrome.tabs.onActivated.fire({windowId: 1, tabId: ++tabId});
  assert.equal(alarm, undefined);
  assert.equal(ports.length, 5);
});
