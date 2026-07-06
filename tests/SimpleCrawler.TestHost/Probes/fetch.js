// The links exist nowhere in the served HTML: this fetch()es a same-origin JSON document and builds the
// anchors at runtime, exercising the JS engines' network-backed fetch (and Response.json()). The Astro SPAs
// bundle their data via eager glob, so this is the only probe that proves a real runtime fetch renders.
fetch('/links.json')
    .then(function (response) { return response.json(); })
    .then(function (links) {
        var app = document.getElementById('app');
        for (var i = 0; i < links.length; i++) {
            var anchor = document.createElement('a');
            anchor.setAttribute('href', links[i].href);
            anchor.textContent = links[i].name;
            app.appendChild(anchor);
        }
    });
