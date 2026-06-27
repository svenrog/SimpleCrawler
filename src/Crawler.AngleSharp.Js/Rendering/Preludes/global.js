// The bundle reaches the DOM through window/self; both are just the global object here.
// structuredClone falls back to a JSON round-trip since the bundle only clones plain data.
var window = globalThis;
var self = globalThis;
globalThis.structuredClone = globalThis.structuredClone || function (v) {
  return v === undefined ? undefined : JSON.parse(JSON.stringify(v));
};
