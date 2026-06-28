// Assigns each link through the anchor `href` *property* on a freshly created anchor (a router/helmet does
// this) and reads the reflected protocol/host/pathname back. Guards the setter that ran `new Uri("")` on the
// element's own empty href and threw "Invalid URI: The URI is empty." (prep.öob.se).
(function () {
    var links = window.__links__;
    var app = document.getElementById('app');
    for (var i = 0; i < links.length; i++) {
        var anchor = document.createElement('a');
        anchor.href = links[i].href;
        if (!anchor.protocol || !anchor.host || !anchor.pathname) continue;
        anchor.textContent = links[i].name;
        app.appendChild(anchor);
    }
})();
