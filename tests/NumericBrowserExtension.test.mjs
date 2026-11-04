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
    action: {onClicked: event()},
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

test("a durable setup tab enrolls only after approval for its unchanged opener", async () => {
  function button() {
    const click = event();
    return { disabled: true, addEventListener(type, fn) { assert.equal(type, "click"); click.addListener(fn); }, click };
  }
  for (const outcome of ["approved", "denied", "source moved", "source closed"]) {
    let answer;
    const decision = new Promise(resolve => { answer = resolve; });
    const entries = {};
    const controls = { "#site": {}, "#status": {}, "#privacy": {}, "#toggle": button(), "#done": button() };
    globalThis.document = { documentElement: {}, querySelector: id => controls[id] };
    let source = { id: 8, windowId: 1, url: topSite+"/private-page" }, grant = false, requested;
    const closed = [], activated = [];
    globalThis.chrome = {
      i18n: { getMessage: name => name, getUILanguage: () => "en" },
      tabs: { getCurrent: async () => ({ id: 9, windowId: 1, openerTabId: 8 }),
        get: async id => { assert.equal(id, 8); if (!source) throw Error("closed"); return source; },
        update: async (id, props) => { assert.deepEqual(props, { active: true }); activated.push(id); },
        remove: async id => closed.push(id), onRemoved: event(), onUpdated: event() },
      permissions: { contains: async () => grant,
        request: async permission => { requested = permission; grant = await decision; return grant; },
        remove: async () => { grant = false; } },
      storage: { local: { get: async () => entries, set: async changed => Object.assign(entries, changed),
        remove: async key => { delete entries[key]; } } }
    };
    await import("../browser-extension/setup.js?case="+encodeURIComponent(outcome));
    assert.equal(controls["#site"].textContent, topSite);
    assert.equal(controls["#toggle"].disabled, false);
    const click = controls["#toggle"].click.fire();
    assert.deepEqual(requested, { origins: [topSite+"/*"] }, "permission requested directly in the click");
    assert.deepEqual(entries, {}, "permission dialog is still open");
    if (outcome === "source moved") source.url = "https://other.example/private";
    if (outcome === "source closed") source = null;
    answer(outcome !== "denied");
    await click;
    assert.deepEqual(entries, outcome === "approved" ? {["site:"+topSite]: true} : {});
    assert.equal(grant, outcome === "approved");
    if (outcome === "approved") {
      assert.equal(controls["#status"].textContent, "enabled");
      await controls["#toggle"].click.fire();
      assert.deepEqual(entries, {});
      assert.equal(grant, false);
      await controls["#done"].click.fire();
      assert.deepEqual(activated, [8]);
      assert.deepEqual(closed, [9]);
    }
  }
});

test("native transport follows enrollment, while field access follows foreground permission", async () => {
  const entries = {};
  let granted = true, closed = false, opens = 0;
  const eventPort = {onMessage:event(), onDisconnect:event(), disconnect(){ closed = true; }};
  globalThis.chrome = {
    action:{onClicked:event()}, runtime:{onStartup:event(),onInstalled:event(),connectNative(){opens++;return eventPort;}},
    alarms:{onAlarm:event(),clear:async()=>true,get:async()=>undefined},
    tabs:{onUpdated:event(),onRemoved:event(),onActivated:event()},
    windows:{onFocusChanged:event()}, permissions:{onAdded:event(),onRemoved:event(),contains:async()=>granted},
    storage:{onChanged:event(),local:{get:async()=>entries,remove:async keys=>keys.forEach(k=>delete entries[k])}}
  };
  await import("../browser-extension/worker.js?lifetime");
  await chrome.runtime.onStartup.fire();
  assert.equal(opens,0);
  entries["site:"+topSite]=true;
  await chrome.storage.onChanged.fire({}, "local");
  assert.equal(opens,1);
  await chrome.tabs.onActivated.fire({});
  await chrome.windows.onFocusChanged.fire(-1);
  assert.equal(closed,false);
  granted=false;
  await chrome.permissions.onRemoved.fire();
  assert.equal(closed,true);
  assert.equal(opens,1);
});
