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
    Element.prototype.querySelector = function (sel) { return querySelectorAll(this, sel)[0] || null; };
    Element.prototype.querySelectorAll = function (sel) { return querySelectorAll(this, sel); };
    Element.prototype.closest = function () { return null; };
    Element.prototype.getBoundingClientRect = function () { return { top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 }; };
    Element.prototype.contains = function (n) { while (n) { if (n === this) return true; n = n.parentNode; } return false; };
    Object.defineProperty(Element.prototype, 'relList', {
        get: function () { return { supports: function () { return true; }, add: function () { }, remove: function () { }, toggle: function () { return false; }, contains: function () { return false; } }; }
    });
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
    Object.defineProperty(Element.prototype, 'sheet', {
        get: function () {
            if (this.localName !== 'style') return null;
            if (!this._sheet) {
                var rules = [];
                this._sheet = {
                    cssRules: rules, rules: rules, ownerNode: this,
                    insertRule: function (rule, index) { var i = index == null ? rules.length : index; rules.splice(i, 0, { cssText: rule }); return i; },
                    deleteRule: function (index) { rules.splice(index, 1); }
                };
            }
            return this._sheet;
        }
    });

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
        this.styleSheets = [];
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
    global.MutationObserver = function () { this.observe = function () { }; this.disconnect = function () { }; this.takeRecords = function () { return []; }; };

    function resolveUrl(u, base) {
        u = String(u == null ? '' : u);
        if (/^[a-zA-Z][\w+.-]*:\/\//.test(u)) return u;
        var b = String(base || global.location.href || 'http://localhost/');
        var bm = b.match(/^([a-zA-Z][\w+.-]*:\/\/[^\/?#]*)([^?#]*)/) || [];
        var origin = bm[1] || 'http://localhost';
        if (u.charAt(0) === '/') return origin + u;
        if (u.charAt(0) === '#' || u.charAt(0) === '?') return origin + (bm[2] || '/') + u;
        var dir = (bm[2] || '/').replace(/[^\/]*$/, '');
        return origin + dir + u;
    }

    function URLSearchParams(init) {
        var pairs = [];
        if (init && init.charAt(0) === '?') init = init.slice(1);
        if (init) init.split('&').forEach(function (p) {
            if (!p) return;
            var i = p.indexOf('=');
            pairs.push(i < 0 ? [decodeURIComponent(p), ''] : [decodeURIComponent(p.slice(0, i)), decodeURIComponent(p.slice(i + 1))]);
        });
        this.get = function (k) { for (var i = 0; i < pairs.length; i++) if (pairs[i][0] === k) return pairs[i][1]; return null; };
        this.getAll = function (k) { return pairs.filter(function (p) { return p[0] === k; }).map(function (p) { return p[1]; }); };
        this.has = function (k) { return this.get(k) !== null; };
        this.set = function (k, v) { this.delete(k); pairs.push([k, String(v)]); };
        this.append = function (k, v) { pairs.push([k, String(v)]); };
        this.delete = function (k) { pairs = pairs.filter(function (p) { return p[0] !== k; }); };
        this.forEach = function (cb) { pairs.forEach(function (p) { cb(p[1], p[0]); }); };
        this.entries = function () {
            var i = 0;
            var it = { next: function () { return i < pairs.length ? { value: pairs[i++], done: false } : { value: undefined, done: true }; } };
            it[Symbol.iterator] = function () { return it; };
            return it;
        };
        this.keys = function () { return pairs.map(function (p) { return p[0]; })[Symbol.iterator](); };
        this.values = function () { return pairs.map(function (p) { return p[1]; })[Symbol.iterator](); };
        this[Symbol.iterator] = this.entries;
        this.toString = function () { return pairs.map(function (p) { return encodeURIComponent(p[0]) + '=' + encodeURIComponent(p[1]); }).join('&'); };
    }
    global.URLSearchParams = URLSearchParams;

    function URL(url, base) {
        var abs = resolveUrl(url, base);
        var m = abs.match(/^([a-zA-Z][\w+.-]*:)\/\/([^\/?#]*)([^?#]*)(\?[^#]*)?(#.*)?$/) || [];
        this.href = abs;
        this.protocol = m[1] || '';
        this.host = m[2] || '';
        this.hostname = (m[2] || '').split(':')[0];
        this.port = (m[2] || '').split(':')[1] || '';
        this.pathname = m[3] || '/';
        this.search = m[4] || '';
        this.hash = m[5] || '';
        this.origin = this.protocol + '//' + this.host;
        this.searchParams = new URLSearchParams(this.search);
    }
    URL.prototype.toString = function () { return this.href; };
    global.URL = URL;

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

    var RAWTEXT = { script: 1, style: 1 };
    var NAMED = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ', copy: '©', reg: '®', trade: '™', hellip: '…', mdash: '—', ndash: '–', lsquo: '‘', rsquo: '’', ldquo: '“', rdquo: '”', laquo: '«', raquo: '»', deg: '°', plusmn: '±', times: '×', divide: '÷', micro: 'µ', euro: '€', pound: '£', cent: '¢', yen: '¥', sect: '§', para: '¶', middot: '·', bull: '•', frac12: '½', frac14: '¼', frac34: '¾', sup2: '²', sup3: '³' };
    var _RE_TAGNAME = /[a-zA-Z][a-zA-Z0-9:_-]*/y;
    var _RE_WS = /[\t\n\f\r ]+/y;
    var _RE_ATTRNAME = /[^\t\n\f\r \/>"'<=]+/y;
    var _RE_BAREVAL = /[^\t\n\f\r >]*/y;

    function decodeEntities(s) {
        if (s == null || s.indexOf('&') < 0) return s;
        return s.replace(/&#(x?[0-9a-fA-F]+);|&([a-zA-Z][a-zA-Z0-9]*);/g, function (m, num, name) {
            if (num != null) {
                var code = num.charAt(0) === 'x' || num.charAt(0) === 'X' ? parseInt(num.slice(1), 16) : parseInt(num, 10);
                return code > 0 && isFinite(code) ? String.fromCharCode(code) : m;
            }
            return NAMED.hasOwnProperty(name) ? NAMED[name] : m;
        });
    }

    function indexOfCI(haystack, needle, from) {
        var n = needle.length, hl = haystack.length;
        for (var p = from; p <= hl - n; p++) {
            for (var q = 0; q < n; q++) {
                var c = haystack.charAt(p + q);
                if (c !== needle.charAt(q) && c.toLowerCase() !== needle.charAt(q)) break;
                if (q === n - 1) return p;
            }
        }
        return -1;
    }

    // Pragmatic, not spec-complete: enough HTML to host a client-rendered bundle and re-serialize its
    // anchors. It tolerates common malformances and degrades to fewer links rather than crashing.
    function parseHTML(input) {
        input = input == null ? '' : String(input);
        var len = input.length;

        var root = new Element('html');
        var head = new Element('head');
        var body = new Element('body');
        root.appendChild(head);
        root.appendChild(body);

        var open = [body];

        function cur() { return open[open.length - 1]; }
        function appendText(parent, text) {
            var last = parent.childNodes[parent.childNodes.length - 1];
            if (last && last.nodeType === TEXT_NODE) last.data += text;
            else parent.appendChild(new Text(text));
        }

        var i = 0;
        while (i < len) {
            var ch = input.charAt(i);
            if (ch !== '<') {
                var textEnd = input.indexOf('<', i);
                if (textEnd < 0) textEnd = len;
                appendText(cur(), decodeEntities(input.slice(i, textEnd)));
                i = textEnd;
                continue;
            }

            if (input.slice(i, i + 4) === '<!--') {
                var cEnd = input.indexOf('-->', i + 4);
                cur().appendChild(new Comment(input.slice(i + 4, cEnd < 0 ? len : cEnd)));
                i = cEnd < 0 ? len : cEnd + 3;
                continue;
            }
            if (input.charAt(i + 1) === '!' || input.charAt(i + 1) === '?') {
                var declEnd = input.indexOf('>', i);
                i = declEnd < 0 ? len : declEnd + 1;
                continue;
            }
            if (input.charAt(i + 1) === '/') {
                _RE_TAGNAME.lastIndex = i + 2;
                var tm = _RE_TAGNAME.exec(input);
                if (tm) {
                    var closeName = tm[0].toLowerCase();
                    for (var k = open.length - 1; k > 0; k--) {
                        if (open[k].localName === closeName) { open.length = k; break; }
                    }
                }
                var slashEnd = input.indexOf('>', i);
                i = slashEnd < 0 ? len : slashEnd + 1;
                continue;
            }

            _RE_TAGNAME.lastIndex = i + 1;
            var sm = _RE_TAGNAME.exec(input);
            if (!sm) { appendText(cur(), '<'); i++; continue; }
            var tag = sm[0].toLowerCase();
            var j = _RE_TAGNAME.lastIndex;
            var attrs = null;
            var selfClosed = false;

            while (j < len) {
                _RE_WS.lastIndex = j;
                if (_RE_WS.exec(input)) j = _RE_WS.lastIndex;
                if (j >= len) break;
                var atC = input.charAt(j);
                if (atC === '>') { j++; break; }
                if (atC === '/' && input.charAt(j + 1) === '>') { selfClosed = true; j += 2; break; }
                _RE_ATTRNAME.lastIndex = j;
                var am = _RE_ATTRNAME.exec(input);
                if (!am) { j++; continue; }
                var an = am[0].toLowerCase();
                j = _RE_ATTRNAME.lastIndex;
                _RE_WS.lastIndex = j;
                if (_RE_WS.exec(input)) j = _RE_WS.lastIndex;
                var val = '';
                if (input.charAt(j) === '=') {
                    j++;
                    _RE_WS.lastIndex = j;
                    if (_RE_WS.exec(input)) j = _RE_WS.lastIndex;
                    var quote = input.charAt(j);
                    if (quote === '"' || quote === "'") {
                        var qEnd = input.indexOf(quote, j + 1);
                        val = decodeEntities(qEnd < 0 ? input.slice(j + 1) : input.slice(j + 1, qEnd));
                        j = qEnd < 0 ? len : qEnd + 1;
                    } else {
                        _RE_BAREVAL.lastIndex = j;
                        var bm = _RE_BAREVAL.exec(input);
                        val = decodeEntities(bm ? bm[0] : '');
                        j = _RE_BAREVAL.lastIndex;
                    }
                }
                (attrs || (attrs = {}))[an] = val;
            }

            if (tag === 'html') {
                if (attrs) for (var ha in attrs) if (attrs.hasOwnProperty(ha)) root._attrs[ha] = attrs[ha];
                i = j; continue;
            }
            if (tag === 'head') {
                open = [head];
                if (attrs) for (var he in attrs) if (attrs.hasOwnProperty(he)) head._attrs[he] = attrs[he];
                i = j; continue;
            }
            if (tag === 'body') {
                open = [body];
                if (attrs) for (var bo in attrs) if (attrs.hasOwnProperty(bo)) body._attrs[bo] = attrs[bo];
                i = j; continue;
            }

            var el = new Element(tag);
            if (attrs) for (var key in attrs) if (attrs.hasOwnProperty(key)) el._attrs[key] = attrs[key];

            if (RAWTEXT[tag]) {
                var rawFrom = j;
                var rawTo = indexOfCI(input, '</' + tag, rawFrom);
                var raw = rawTo < 0 ? input.slice(rawFrom) : input.slice(rawFrom, rawTo);
                if (raw) el.appendChild(new Text(raw));
                var rawGt = rawTo < 0 ? len : input.indexOf('>', rawTo);
                i = rawGt < 0 ? len : rawGt + 1;
                cur().appendChild(el);
                continue;
            }

            cur().appendChild(el);
            if (!VOID[tag] && !selfClosed) open.push(el);
            i = j;
        }

        doc.documentElement = root;
        doc.head = head;
        doc.body = body;
        return root;
    }

    function collectScripts() {
        var out = [];
        if (!doc.documentElement) return out;
        function walk(n) {
            for (var i = 0; i < n.childNodes.length; i++) {
                var c = n.childNodes[i];
                if (c.nodeType !== ELEMENT_NODE) continue;
                if (c.localName === 'script') {
                    var type = c._attrs.type || '';
                    if (type && type !== 'text/javascript' && type !== 'module' && type !== 'application/javascript') {
                        walk(c); continue;
                    }
                    out.push({ module: type === 'module', external: !!c._attrs.src, src: c._attrs.src || '', text: textOf(c) });
                }
                walk(c);
            }
        }
        walk(doc.documentElement);
        return out;
    }

    function pumpTasks() {
        if (!tasks.length) return 0;
        var batch = tasks; tasks = [];
        for (var i = 0; i < batch.length; i++) { try { batch[i](); } catch (e) { } }
        return tasks.length;
    }

    global.__crawlerSetLocation = function (url) { applyUrl(url); };
    global.__crawlerLoadHtml = function (html) { parseHTML(html); };
    global.__crawlerCollectScripts = function () { return JSON.stringify(collectScripts()); };
    global.__crawlerPending = function () { return tasks.length; };
    global.__crawlerPump = function () { return pumpTasks(); };
    global.__crawlerSerialize = function () { return doc.documentElement ? serializeNode(doc.documentElement) : ''; };
})(typeof globalThis !== 'undefined' ? globalThis : this);
