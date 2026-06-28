// Stores a cyclic expando on a DOM node (React 18 stashes fibers there) and assigns a freshly created
// element's onload handler (webpack's chunk loader does `script.onload = fn`). Guards the expando side table
// and onload/onerror living on the base JsElement (exposing them on the derived wrapper threw
// MissingMemberException on V8, breaking dynamic chunk loading on nille.no).
(function () {
    var probe = document.createElement('div');
    var cyclic = {}; cyclic.self = cyclic;
    probe.__fiber = cyclic;
    document.__root = probe;
    var script = document.createElement('script');
    script.onload = function () { };
    var ok = probe.__fiber === cyclic
        && probe.__fiber.self === probe.__fiber
        && document.__root === probe
        && typeof script.onload === 'function';
    if (!ok) return;
    var links = window.__links__;
    var app = document.getElementById('app');
    for (var i = 0; i < links.length; i++) {
        var anchor = document.createElement('a');
        anchor.setAttribute('href', links[i].href);
        anchor.textContent = links[i].name;
        app.appendChild(anchor);
    }
})();
