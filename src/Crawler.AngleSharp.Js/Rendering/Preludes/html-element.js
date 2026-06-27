// Bundles extend HTMLElement via `class X extends HTMLElement`, which V8/ClearScript cannot do with a
// CLR host type (no JS prototype). Never instantiated; stubs suffice.
globalThis.HTMLElement = globalThis.HTMLElement || class HTMLElement {
  addEventListener() {}
  removeEventListener() {}
  dispatchEvent() { return true; }
  attachShadow() { return this; }
};
globalThis.HTMLScriptElement = globalThis.HTMLScriptElement || class HTMLScriptElement extends HTMLElement {};
