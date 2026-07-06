// Drains a Map by iterating map.keys() and deleting each key in the loop body — spec-legal, and the shape a
// registry uses to fire-and-forget its entries. Guards the engine compat shim: Jint's native keys()/values()
// iterators walk the live backing list and throw a CLR "Collection was modified" on mid-iteration mutation,
// aborting the render; the shim routes both views through the tolerant entries iterator. V8 has no such bug.
(function () {
    var data = window.__links__;
    var registry = new Map();
    for (var i = 0; i < data.length; i++) {
        registry.set('k' + i, data[i]);
    }
    var collected = [];
    for (var key of registry.keys()) {
        collected.push(registry.get(key));
        registry.delete(key);
    }
    var app = document.getElementById('app');
    for (var j = 0; j < collected.length; j++) {
        var anchor = document.createElement('a');
        anchor.setAttribute('href', collected[j].href);
        anchor.textContent = collected[j].name;
        app.appendChild(anchor);
    }
})();
