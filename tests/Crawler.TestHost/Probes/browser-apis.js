// Appends one /features/* link per browser API the engine implements (each probe formerly lived in the test
// host's Astro feature-nav, contrived shared code that didn't belong in the real-framework SPAs). Asserting
// all of them render guards the navigator/storage/observer/crypto/customElements/cookie surface the JS render
// engines must expose for production bundles.
(function () {
    var features = window.__features__;
    var probes = {
        'geolocation': function () {
            return !!(navigator.geolocation && typeof navigator.geolocation.getCurrentPosition === 'function');
        },
        'local-storage': function () {
            localStorage.setItem('crawler-probe', 'ok');
            var ok = localStorage.getItem('crawler-probe') === 'ok';
            localStorage.removeItem('crawler-probe');
            return ok;
        },
        'intersection-observer': function () {
            var observer = new IntersectionObserver(function () { });
            observer.observe(document.documentElement);
            observer.disconnect();
            return true;
        },
        'cookies': function () {
            document.cookie = 'crawler-probe=ok; path=/';
            return document.cookie.indexOf('crawler-probe=ok') !== -1;
        },
        'session-storage': function () {
            sessionStorage.setItem('crawler-probe', 'ok');
            var ok = sessionStorage.getItem('crawler-probe') === 'ok';
            sessionStorage.removeItem('crawler-probe');
            return ok;
        },
        'match-media': function () {
            if (typeof window.matchMedia !== 'function') return false;
            return typeof window.matchMedia('(min-width: 0px)').matches === 'boolean';
        },
        'resize-observer': function () {
            var observer = new ResizeObserver(function () { });
            observer.observe(document.documentElement);
            observer.disconnect();
            return true;
        },
        'mutation-observer': function () {
            var observer = new MutationObserver(function () { });
            observer.observe(document.documentElement, { childList: true });
            observer.disconnect();
            return true;
        },
        'structured-clone': function () {
            return structuredClone({ ok: true }).ok === true;
        },
        'crypto-random-uuid': function () {
            if (typeof crypto === 'undefined' || !crypto || typeof crypto.randomUUID !== 'function') return false;
            return typeof crypto.randomUUID() === 'string';
        },
        'custom-elements': function () {
            return typeof customElements !== 'undefined' && !!customElements && typeof customElements.define === 'function';
        },
        'node-list': function () {
            if (typeof NodeList === 'undefined') return false;
            var list = document.querySelectorAll('*');
            return list instanceof NodeList && typeof list.item === 'function';
        },
        'element-traversal': function () {
            var track = document.createElement('div');
            var a = document.createElement('span');
            a.setAttribute('class', 'slide');
            var b = document.createElement('span');
            b.setAttribute('class', 'slide');
            track.appendChild(a);
            track.appendChild(b);
            return track.firstElementChild === a
                && track.lastElementChild === b
                && a.nextElementSibling === b
                && b.previousElementSibling === a
                && a.parentElement === track
                && track.childElementCount === 2
                && track.getElementsByClassName('slide').length === 2;
        }
    };
    var app = document.getElementById('app');
    for (var i = 0; i < features.length; i++) {
        var feature = features[i];
        var probe = probes[feature.key];
        if (!probe) continue;
        var ok = false;
        try { ok = probe(); } catch (e) { ok = false; }
        if (!ok) continue;
        var anchor = document.createElement('a');
        anchor.setAttribute('href', feature.href);
        anchor.textContent = feature.name;
        app.appendChild(anchor);
    }
})();
