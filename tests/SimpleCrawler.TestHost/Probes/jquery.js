// Mimics jQuery's UMD: a load-time IIFE runs feature detection against the DOM, then assigns window.jQuery;
// a later "webpack external" reads the global to render. Guards the surface jQuery touches during init
// (createDocumentFragment, implementation.createHTMLDocument, loosely reflected script async/defer/type,
// an attribute read back off `attributes` by name) — missing any of it threw before the assignment, so
// later bundles failed with "jQuery is not defined".
(function (global, factory) {
    global.jQuery = global.$ = factory(global);
})(window, function (window) {
    var fragment = document.createDocumentFragment();
    var support = document.implementation.createHTMLDocument('');
    var script = document.createElement('script');
    script.async = 1;
    script.defer = 1;
    script.type = 'text/javascript';
    var probe = document.createElement('div');
    probe.setAttribute('onsubmit', 't');
    // Dereferencing the named lookup is the guard — jQuery reads a legacy property that is undefined in a
    // browser too, so what an absent NamedNodeMap entry costs is the throw, not the value.
    var reflected = probe.attributes['onsubmit'].value === 't';
    if (!fragment || !support || !script.async || !script.defer || script.type !== 'text/javascript' || !reflected)
        throw new Error('jQuery feature detection failed');
    return { fn: { jquery: '3.x' } };
});

(function () {
    if (typeof jQuery === 'undefined' || window.$ !== jQuery) return;
    var links = window.__links__;
    var app = document.getElementById('app');
    for (var i = 0; i < links.length; i++) {
        var anchor = document.createElement('a');
        anchor.setAttribute('href', links[i].href);
        anchor.textContent = links[i].name;
        app.appendChild(anchor);
    }
})();
