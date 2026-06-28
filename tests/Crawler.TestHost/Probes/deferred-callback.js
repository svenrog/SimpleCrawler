// Renders from inside a setTimeout callback whose callee binds a destructuring parameter with a default that
// reads outer scope. Guards Jint deferred callbacks running through the engine's evaluation context (the raw
// marshalled Func delegate threw a bare NullReferenceException on the default; blanked nille.no).
(function () {
    var data = window.__links__;
    function render({ items = data } = {}) {
        var app = document.getElementById('app');
        for (var i = 0; i < items.length; i++) {
            var anchor = document.createElement('a');
            anchor.setAttribute('href', items[i].href);
            anchor.textContent = items[i].name;
            app.appendChild(anchor);
        }
    }
    setTimeout(function () { render(); }, 0);
})();
