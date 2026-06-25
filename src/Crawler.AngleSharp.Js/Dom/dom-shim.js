// A minimal, serialization-oriented DOM sufficient to run a client-rendered SPA bundle
// (vanilla or Preact/React-style reconcilers) far enough to materialize its anchors.
// It is not spec-complete; it implements the subset of the DOM that rendering touches,
// then serializes the resulting tree back to HTML for the static link extractor to parse.
(function (global) {
    var ELEMENT_NODE = 1, TEXT_NODE = 3, COMMENT_NODE = 8, DOCUMENT_NODE = 9, FRAGMENT_NODE = 11;
    var VOID = { area: 1, base: 1, br: 1, col: 1, embed: 1, hr: 1, img: 1, input: 1, link: 1, meta: 1, param: 1, source: 1, track: 1, wbr: 1 };

    var tasks = [];
    function enqueue(cb) { if (typeof cb === 'function') tasks.push(cb); return tasks.length; }

    global.queueMicrotask = function (cb) { enqueue(cb); };
    global.setTimeout = function (cb) { return enqueue(cb); };
    global.clearTimeout = function () { };
    global.setInterval = function () { return 0; };
    global.clearInterval = function () { };
    global.requestAnimationFrame = function (cb) { return enqueue(function () { cb(0); }); };
    global.cancelAnimationFrame = function () { };

    function styleObject() {
        var store = {};
        var handler = {
            get: function (t, k) {
                if (k === 'setProperty') return function (n, v) { store[n] = v; };
                if (k === 'removeProperty') return function (n) { delete store[n]; };
                if (k === 'getPropertyValue') return function (n) { return store[n] || ''; };
                if (k === 'cssText') {
                    var out = [];
                    for (var p in store) if (store.hasOwnProperty(p)) out.push(p + ': ' + store[p]);
                    return out.join('; ');
                }
                if (k === '_store') return store;
                return store[k] != null ? store[k] : '';
            },
            set: function (t, k, v) {
                if (k === 'cssText') { for (var p in store) delete store[p]; if (v) parseCss(v, store); return true; }
                store[k] = v; return true;
            }
        };
        return new Proxy({}, handler);
    }
    function parseCss(text, store) {
        var parts = String(text).split(';');
        for (var i = 0; i < parts.length; i++) {
            var idx = parts[i].indexOf(':');
            if (idx > 0) store[parts[i].slice(0, idx).trim()] = parts[i].slice(idx + 1).trim();
        }
    }

    function Node(type) { this.nodeType = type; this.childNodes = []; this.parentNode = null; }
    Node.prototype.appendChild = function (child) { return this.insertBefore(child, null); };
    Node.prototype.insertBefore = function (child, ref) {
        if (child.nodeType === FRAGMENT_NODE) {
            var kids = child.childNodes.slice();
            for (var i = 0; i < kids.length; i++) this.insertBefore(kids[i], ref);
            return child;
        }
        if (child.parentNode) child.parentNode.removeChild(child);
        var at = ref ? this.childNodes.indexOf(ref) : -1;
        if (at < 0) this.childNodes.push(child); else this.childNodes.splice(at, 0, child);
        child.parentNode = this;
        return child;
    };
    Node.prototype.removeChild = function (child) {
        var at = this.childNodes.indexOf(child);
        if (at >= 0) { this.childNodes.splice(at, 1); child.parentNode = null; }
        return child;
    };
    Node.prototype.replaceChild = function (n, o) { this.insertBefore(n, o); this.removeChild(o); return o; };
    Node.prototype.remove = function () { if (this.parentNode) this.parentNode.removeChild(this); };
    Object.defineProperty(Node.prototype, 'firstChild', { get: function () { return this.childNodes[0] || null; } });
    Object.defineProperty(Node.prototype, 'lastChild', { get: function () { return this.childNodes[this.childNodes.length - 1] || null; } });
    Object.defineProperty(Node.prototype, 'nextSibling', {
        get: function () { if (!this.parentNode) return null; var s = this.parentNode.childNodes, i = s.indexOf(this); return i >= 0 ? (s[i + 1] || null) : null; }
    });
    Object.defineProperty(Node.prototype, 'previousSibling', {
        get: function () { if (!this.parentNode) return null; var s = this.parentNode.childNodes, i = s.indexOf(this); return i > 0 ? s[i - 1] : null; }
    });

    function Text(data) { Node.call(this, TEXT_NODE); this.data = data == null ? '' : String(data); }
    Text.prototype = Object.create(Node.prototype);
    Object.defineProperty(Text.prototype, 'nodeValue', { get: function () { return this.data; }, set: function (v) { this.data = v == null ? '' : String(v); } });
    Object.defineProperty(Text.prototype, 'textContent', { get: function () { return this.data; }, set: function (v) { this.data = v == null ? '' : String(v); } });

    function Comment(data) { Node.call(this, COMMENT_NODE); this.data = data == null ? '' : String(data); }
    Comment.prototype = Object.create(Node.prototype);

    function Element(tag, ns) {
        Node.call(this, ELEMENT_NODE);
        this.localName = String(tag).toLowerCase();
        this.tagName = this.localName.toUpperCase();
        this.nodeName = this.tagName;
        this.namespaceURI = ns || 'http://www.w3.org/1999/xhtml';
        this._attrs = {};
        this._listeners = {};
        this.style = styleObject();
        this._innerHTML = null;
    }
    Element.prototype = Object.create(Node.prototype);
    Element.prototype.setAttribute = function (name, value) { this._attrs[name] = value == null ? '' : String(value); };
    Element.prototype.setAttributeNS = function (ns, name, value) { this.setAttribute(name, value); };
    Element.prototype.getAttribute = function (name) { return this._attrs.hasOwnProperty(name) ? this._attrs[name] : null; };
    Element.prototype.removeAttribute = function (name) { delete this._attrs[name]; };
    Element.prototype.removeAttributeNS = function (ns, name) { delete this._attrs[name]; };
    Element.prototype.hasAttribute = function (name) { return this._attrs.hasOwnProperty(name); };
    Element.prototype.addEventListener = function (t, cb) { (this._listeners[t] = this._listeners[t] || []).push(cb); };
    Element.prototype.removeEventListener = function () { };
    Element.prototype.dispatchEvent = function () { return true; };
    Element.prototype.setAttributeNode = function () { };
    Element.prototype.getElementsByTagName = function (tag) { var out = []; collectByTag(this, String(tag).toLowerCase(), out); return out; };
    Element.prototype.contains = function (n) { while (n) { if (n === this) return true; n = n.parentNode; } return false; };
    Element.prototype.appendChild = function (child) { this._innerHTML = null; return Node.prototype.appendChild.call(this, child); };
    Element.prototype.insertBefore = function (child, ref) { this._innerHTML = null; return Node.prototype.insertBefore.call(this, child, ref); };
    Object.defineProperty(Element.prototype, 'id', { get: function () { return this._attrs.id || ''; }, set: function (v) { this._attrs.id = String(v); } });
    Object.defineProperty(Element.prototype, 'className', { get: function () { return this._attrs['class'] || ''; }, set: function (v) { this._attrs['class'] = String(v); } });
    Object.defineProperty(Element.prototype, 'children', {
        get: function () { return this.childNodes.filter(function (n) { return n.nodeType === ELEMENT_NODE; }); }
    });
    Object.defineProperty(Element.prototype, 'innerHTML', {
        get: function () { return this._innerHTML != null ? this._innerHTML : serializeChildren(this); },
        set: function (v) { this.childNodes = []; this._innerHTML = v == null ? '' : String(v); }
    });
    Object.defineProperty(Element.prototype, 'textContent', {
        get: function () { return textOf(this); },
        set: function (v) { this.childNodes = []; this._innerHTML = null; if (v != null && v !== '') this.appendChild(new Text(v)); }
    });
    Object.defineProperty(Element.prototype, 'outerHTML', { get: function () { return serializeNode(this); } });

    function collectByTag(node, tag, out) {
        for (var i = 0; i < node.childNodes.length; i++) {
            var c = node.childNodes[i];
            if (c.nodeType === ELEMENT_NODE) { if (c.localName === tag) out.push(c); collectByTag(c, tag, out); }
        }
    }
    function textOf(node) {
        if (node.nodeType === TEXT_NODE) return node.data;
        var s = '';
        for (var i = 0; i < node.childNodes.length; i++) s += textOf(node.childNodes[i]);
        return s;
    }

    function HTMLDocument() {
        Node.call(this, DOCUMENT_NODE);
        this.documentElement = null;
        this.head = null;
        this.body = null;
        this.defaultView = global;
    }
    HTMLDocument.prototype = Object.create(Node.prototype);
    HTMLDocument.prototype.createElement = function (tag) { return new Element(tag); };
    HTMLDocument.prototype.createElementNS = function (ns, tag) { return new Element(tag, ns); };
    HTMLDocument.prototype.createTextNode = function (data) { return new Text(data); };
    HTMLDocument.prototype.createComment = function (data) { return new Comment(data); };
    HTMLDocument.prototype.createDocumentFragment = function () { return new Node(FRAGMENT_NODE); };
    HTMLDocument.prototype.getElementById = function (id) { return walkFind(this.documentElement, function (e) { return e._attrs.id === id; }); };
    HTMLDocument.prototype.getElementsByTagName = function (tag) { var out = []; if (this.documentElement) collectByTag(this.documentElement, String(tag).toLowerCase(), out); return out; };
    HTMLDocument.prototype.querySelector = function (sel) { return querySelectorAll(this, sel)[0] || null; };
    HTMLDocument.prototype.querySelectorAll = function (sel) { return querySelectorAll(this, sel); };
    HTMLDocument.prototype.addEventListener = function () { };
    HTMLDocument.prototype.removeEventListener = function () { };
    HTMLDocument.prototype.createEvent = function () { return { initEvent: function () { } }; };

    function walkFind(node, pred) {
        if (!node) return null;
        if (node.nodeType === ELEMENT_NODE && pred(node)) return node;
        for (var i = 0; i < node.childNodes.length; i++) {
            var r = walkFind(node.childNodes[i], pred);
            if (r) return r;
        }
        return null;
    }
    function querySelectorAll(root, sel) {
        var el = root.documentElement || root;
        var out = [];
        sel = String(sel).trim();
        var idM = sel.match(/^#([\w-]+)$/);
        var attrM = sel.match(/^(\w+)?\[([\w-]+)(?:[~|]?=["']?([^"'\]]*)["']?)?\]$/);
        walk(el);
        function walk(n) {
            if (n.nodeType === ELEMENT_NODE && matches(n)) out.push(n);
            for (var i = 0; i < n.childNodes.length; i++) walk(n.childNodes[i]);
        }
        function matches(n) {
            if (idM) return n._attrs.id === idM[1];
            if (attrM) {
                if (attrM[1] && n.localName !== attrM[1].toLowerCase()) return false;
                if (!n._attrs.hasOwnProperty(attrM[2])) return false;
                if (attrM[3] != null && attrM[3] !== '') return n._attrs[attrM[2]] === attrM[3];
                return true;
            }
            return n.localName === sel.toLowerCase();
        }
        return out;
    }

    function escapeAttr(v) { return String(v).replace(/&/g, '&amp;').replace(/"/g, '&quot;'); }
    function escapeText(v) { return String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }
    function serializeChildren(node) {
        if (node._innerHTML != null) return node._innerHTML;
        var s = '';
        for (var i = 0; i < node.childNodes.length; i++) s += serializeNode(node.childNodes[i]);
        return s;
    }
    function serializeNode(node) {
        if (node.nodeType === TEXT_NODE) return escapeText(node.data);
        if (node.nodeType === COMMENT_NODE) return '<!--' + node.data + '-->';
        if (node.nodeType === FRAGMENT_NODE || node.nodeType === DOCUMENT_NODE) return serializeChildren(node);
        var tag = node.localName;
        var s = '<' + tag;
        var style = node.style && node.style._store;
        for (var k in node._attrs) if (node._attrs.hasOwnProperty(k)) s += ' ' + k + '="' + escapeAttr(node._attrs[k]) + '"';
        if (style) { var css = node.style.cssText; if (css && !node._attrs.style) s += ' style="' + escapeAttr(css) + '"'; }
        s += '>';
        if (VOID[tag]) return s;
        s += serializeChildren(node);
        return s + '</' + tag + '>';
    }

    var doc = new HTMLDocument();
    global.document = doc;
    global.window = global;
    global.self = global;
    global.navigator = { userAgent: 'SimpleCrawler', platform: '', language: 'en' };
    global.console = global.console || { log: function () { }, warn: function () { }, error: function () { }, info: function () { }, debug: function () { } };
    global.location = { href: 'http://localhost/', protocol: 'http:', host: 'localhost', hostname: 'localhost', port: '', pathname: '/', search: '', hash: '', origin: 'http://localhost' };
    global.history = {
        pushState: function (s, t, u) { if (u) applyUrl(u); },
        replaceState: function (s, t, u) { if (u) applyUrl(u); },
        go: function () { }, back: function () { }, forward: function () { }, length: 1, state: null
    };
    global.addEventListener = function () { };
    global.removeEventListener = function () { };
    global.dispatchEvent = function () { return true; };
    global.matchMedia = function () { return { matches: false, addListener: function () { }, removeListener: function () { }, addEventListener: function () { }, removeEventListener: function () { } }; };
    global.getComputedStyle = function () { return { getPropertyValue: function () { return ''; } }; };

    function applyUrl(u) {
        try {
            var abs = u;
            if (u.indexOf('http') !== 0) {
                var base = global.location.origin || 'http://localhost';
                abs = u.charAt(0) === '/' ? base + u : base + '/' + u;
            }
            var m = abs.match(/^(https?:)\/\/([^\/?#]+)([^?#]*)(\?[^#]*)?(#.*)?$/);
            if (!m) return;
            var loc = global.location;
            loc.href = abs; loc.protocol = m[1]; loc.host = m[2]; loc.hostname = m[2].split(':')[0];
            loc.port = (m[2].split(':')[1] || ''); loc.pathname = m[3] || '/'; loc.search = m[4] || ''; loc.hash = m[5] || '';
            loc.origin = m[1] + '//' + m[2];
        } catch (e) { }
    }

    function hydrate(spec, parent) {
        var node;
        if (spec.text != null) { node = new Text(spec.text); }
        else if (spec.comment != null) { node = new Comment(spec.comment); }
        else {
            node = new Element(spec.tag);
            if (spec.attrs) for (var k in spec.attrs) if (spec.attrs.hasOwnProperty(k)) node._attrs[k] = spec.attrs[k];
            if (spec.children) for (var i = 0; i < spec.children.length; i++) hydrate(spec.children[i], node);
        }
        if (parent) parent.appendChild(node); else {
            doc.documentElement = node;
            doc.head = node.getElementsByTagName('head')[0] || null;
            doc.body = node.getElementsByTagName('body')[0] || null;
        }
        return node;
    }

    global.__crawler = {
        setLocation: function (url) { applyUrl(url); },
        hydrate: function (json) { hydrate(typeof json === 'string' ? JSON.parse(json) : json, null); },
        finalize: function (maxIters) {
            var iters = 0;
            while (tasks.length && iters++ < maxIters) {
                var batch = tasks; tasks = [];
                for (var i = 0; i < batch.length; i++) { try { batch[i](); } catch (e) { } }
            }
            return serializeNode(doc.documentElement);
        }
    };
})(typeof globalThis !== 'undefined' ? globalThis : this);
