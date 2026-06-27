// A plain JS wrapper rather than the host object: routers reassign pushState/replaceState and set
// scrollRestoration, which a CLR host object rejects as read-only members.
(function () {
  var h = __history;
  globalThis.history = {
    get length() { return h.length; },
    get state() { return h.state; },
    scrollRestoration: 'auto',
    pushState: function (s, t, u) { return h.pushState(s, t, u); },
    replaceState: function (s, t, u) { return h.replaceState(s, t, u); },
    go: function (d) { return h.go(d); },
    back: function () { return h.back(); },
    forward: function () { return h.forward(); }
  };
})();
