// A JS wrapper rather than the host object directly: uuid/nanoid bundles do crypto.randomUUID.bind(crypto),
// and a V8/ClearScript host method has no .bind/.call.
globalThis.crypto = globalThis.crypto || {
  randomUUID: function () { return __crypto.randomUUID(); },
  getRandomValues: function (a) {
    if (a) for (var i = 0; i < a.length; i++) a[i] = Math.floor(Math.random() * 256);
    return a;
  }
};
