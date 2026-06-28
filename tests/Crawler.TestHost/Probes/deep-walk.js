// Renders only if a DOM node reports no own enumerable keys (the browser invariant a deep-walking bundle
// relies on), then runs a real JSON.stringify over it. Guards the Jint StackOverflow where node-wrapper CLR
// getters were reported as enumerable own keys and a walker followed the DOM's cycles forever (ewheels.se).
// The guard returns before JSON.stringify on regression, so it fails as an empty crawl. Jint-only: V8's
// host-object enumeration exposes keys and never matched this invariant, yet it never overflowed.
(function () {
    var node = document.createElement('div');
    var child = document.createElement('span');
    node.appendChild(child);

    if (Object.keys(node).length !== 0) return;
    JSON.stringify(node);

    var links = window.__links__;
    var app = document.getElementById('app');
    for (var i = 0; i < links.length; i++) {
        var anchor = document.createElement('a');
        anchor.setAttribute('href', links[i].href);
        anchor.textContent = links[i].name;
        app.appendChild(anchor);
    }
})();
