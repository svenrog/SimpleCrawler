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
        },
        'has-child-nodes': function () {
            var parent = document.createElement('div');
            if (parent.hasChildNodes() !== false) return false;
            parent.appendChild(document.createElement('span'));
            return parent.hasChildNodes() === true;
        },
        'htmlelement-instanceof': function () {
            return document.createElement('div') instanceof HTMLElement
                && document.body instanceof HTMLElement;
        },
        'base64': function () {
            if (typeof btoa !== 'function' || typeof atob !== 'function') return false;
            return btoa('foobar') === 'Zm9vYmFy' && atob('Zm9vYmFy') === 'foobar';
        },
        'document-referrer': function () {
            return typeof document.referrer === 'string'
                && typeof document.referrer.split('/')[2] !== 'object';
        },
        'document-dispatch-event': function () {
            if (typeof document.dispatchEvent !== 'function' || typeof window.dispatchEvent !== 'function') return false;
            var firedOn = function (target) {
                var fired = false;
                var listener = function () { fired = true; };
                target.addEventListener('crawler-probe', listener);
                var result = target.dispatchEvent(new CustomEvent('crawler-probe', { detail: 1 }));
                target.removeEventListener('crawler-probe', listener);
                return fired && result === true;
            };
            return firedOn(document) && firedOn(window);
        },
        'event-target': function () {
            if (typeof EventTarget !== 'function') return false;
            class Widget extends EventTarget {
                constructor() { super(); this.ready = true; }
            }
            var w = new Widget();
            var fired = false;
            w.addEventListener('crawler-probe', function () { fired = true; });
            w.dispatchEvent(new CustomEvent('crawler-probe', {}));
            return w.ready === true && fired
                && w instanceof Widget && w instanceof EventTarget
                && document instanceof EventTarget;
        },
        'element-attributes': function () {
            var el = document.createElement('div');
            el.setAttribute('data-x', '1');
            el.setAttribute('data-y', '2');
            var attrs = el.attributes;
            if (!attrs || attrs.length !== 2) return false;
            var found = {};
            for (var i = attrs.length; i--;) found[attrs[i].name] = attrs[i].value;
            return found['data-x'] === '1' && found['data-y'] === '2'
                && attrs.getNamedItem('data-x').value === '1';
        },
        'outer-html-setter': function () {
            var parent = document.createElement('div');
            var wrapper = document.createElement('span');
            wrapper.innerHTML = '<a href="/unwrapped">x</a>';
            parent.appendChild(wrapper);
            wrapper.outerHTML = wrapper.innerHTML;
            var a = parent.querySelector('a');
            return !!a && a.getAttribute('href') === '/unwrapped'
                && parent.querySelector('span') === null;
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
