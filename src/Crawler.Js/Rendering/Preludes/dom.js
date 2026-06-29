"use strict";
(() => {
  var __defProp = Object.defineProperty;
  var __export = (target, all) => {
    for (var name in all)
      __defProp(target, name, { get: all[name], enumerable: true });
  };

  // dom/documentRef.ts
  var documentRef = { current: null };

  // dom/Node.ts
  var Node = class {
    constructor(type) {
      this.parentNode = null;
      this.childNodes = [];
      this.nodeType = type;
    }
    get ownerDocument() {
      return documentRef.current;
    }
    appendChild(child) {
      return this.insertBefore(child, null);
    }
    insertBefore(child, ref) {
      if (child.nodeType === 11 /* DocumentFragment */) {
        const kids = child.childNodes.slice();
        for (const k of kids) this.insertBefore(k, ref);
        return child;
      }
      if (child.parentNode) child.parentNode.removeChild(child);
      const at = ref ? this.childNodes.indexOf(ref) : -1;
      if (at < 0) this.childNodes.push(child);
      else this.childNodes.splice(at, 0, child);
      child.parentNode = this;
      return child;
    }
    removeChild(child) {
      const at = this.childNodes.indexOf(child);
      if (at >= 0) {
        this.childNodes.splice(at, 1);
        child.parentNode = null;
      }
      return child;
    }
    replaceChild(n, o) {
      this.insertBefore(n, o);
      this.removeChild(o);
      return o;
    }
    remove() {
      if (this.parentNode) this.parentNode.removeChild(this);
    }
    get firstChild() {
      return this.childNodes[0] || null;
    }
    get lastChild() {
      return this.childNodes[this.childNodes.length - 1] || null;
    }
    get nextSibling() {
      if (!this.parentNode) return null;
      const s = this.parentNode.childNodes;
      const i = s.indexOf(this);
      return i >= 0 ? s[i + 1] || null : null;
    }
    get previousSibling() {
      if (!this.parentNode) return null;
      const s = this.parentNode.childNodes;
      const i = s.indexOf(this);
      return i > 0 ? s[i - 1] : null;
    }
    // ChildNode / ParentNode insertion helpers. Svelte 5's compiled output threads its DOM through
    // anchor.before(node) and target.append(...nodes); a string argument becomes a Text node.
    before(...nodes) {
      const parent = this.parentNode;
      if (!parent) return;
      for (const n of nodes) parent.insertBefore(asNode(n), this);
    }
    after(...nodes) {
      const parent = this.parentNode;
      if (!parent) return;
      const ref = this.nextSibling;
      for (const n of nodes) parent.insertBefore(asNode(n), ref);
    }
    replaceWith(...nodes) {
      const parent = this.parentNode;
      if (!parent) return;
      for (const n of nodes) parent.insertBefore(asNode(n), this);
      parent.removeChild(this);
    }
    append(...nodes) {
      for (const n of nodes) this.appendChild(asNode(n));
    }
    prepend(...nodes) {
      const ref = this.firstChild;
      for (const n of nodes) this.insertBefore(asNode(n), ref);
    }
    cloneNode(deep) {
      const clone = this._shallowClone();
      if (deep) for (const c of this.childNodes) clone.appendChild(c.cloneNode(true));
      return clone;
    }
  };
  function asNode(value) {
    return value instanceof Node ? value : documentRef.current.createTextNode(value);
  }

  // dom/Text.ts
  var Text = class _Text extends Node {
    constructor(data) {
      super(3 /* Text */);
      this.data = data == null ? "" : String(data);
    }
    get nodeValue() {
      return this.data;
    }
    set nodeValue(v) {
      this.data = v == null ? "" : String(v);
    }
    get textContent() {
      return this.data;
    }
    set textContent(v) {
      this.data = v == null ? "" : String(v);
    }
    _shallowClone() {
      return new _Text(this.data);
    }
  };

  // css/CSSStyleDeclaration.ts
  function parseCss(text, store) {
    const parts = String(text).split(";");
    for (const part of parts) {
      const idx = part.indexOf(":");
      if (idx > 0) store[part.slice(0, idx).trim()] = part.slice(idx + 1).trim();
    }
  }
  function createStyleDeclaration() {
    const store = {};
    const handler = {
      get: (_t, k) => {
        if (k === "setProperty") return (n, v2) => {
          store[n] = v2;
        };
        if (k === "removeProperty") return (n) => {
          delete store[n];
        };
        if (k === "getPropertyValue") return (n) => store[n] || "";
        if (k === "cssText") {
          const out = [];
          for (const p in store) if (Object.prototype.hasOwnProperty.call(store, p)) out.push(p + ": " + store[p]);
          return out.join("; ");
        }
        if (k === "_store") return store;
        const v = store[k];
        return v != null ? v : "";
      },
      set: (_t, k, v) => {
        if (k === "cssText") {
          for (const p in store) delete store[p];
          if (v) parseCss(v, store);
          return true;
        }
        store[k] = v;
        return true;
      }
    };
    return new Proxy({}, handler);
  }

  // dom/utils.ts
  function escapeAttr(v) {
    return String(v).replace(/&/g, "&amp;").replace(/"/g, "&quot;");
  }
  function escapeText(v) {
    return String(v).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }
  function collectByTag(node, tag, out) {
    const kids = node.childNodes;
    for (let i = 0; i < kids.length; i++) {
      const c = kids[i];
      if (c.nodeType === 1 /* Element */) {
        if (c.localName === tag) out.push(c);
        collectByTag(c, tag, out);
      }
    }
  }
  function textOf(node) {
    if (node.nodeType === 3 /* Text */) return node.data;
    let s = "";
    for (const c of node.childNodes) s += textOf(c);
    return s;
  }
  function walkFind(node, pred) {
    if (!node) return null;
    if (node.nodeType === 1 /* Element */ && pred(node)) return node;
    for (const c of node.childNodes) {
      const r = walkFind(c, pred);
      if (r) return r;
    }
    return null;
  }

  // selector/querySelector.ts
  function querySelectorAll(root, sel) {
    const el = root.documentElement || root;
    const out = [];
    const s = String(sel).trim();
    const idM = s.match(/^#([\w-]+)$/);
    const attrM = s.match(/^(\w+)?\[([\w-]+)(?:[~|]?=["']?([^"'\]]*)["']?)?\]$/);
    walk(el);
    return out;
    function walk(n) {
      if (n.nodeType === 1 /* Element */ && matches(n)) out.push(n);
      for (const c of n.childNodes) walk(c);
    }
    function matches(n) {
      const e = n;
      if (idM) return e.getAttribute("id") === idM[1];
      if (attrM) {
        if (attrM[1] && e.localName !== attrM[1].toLowerCase()) return false;
        if (!e.hasAttribute(attrM[2])) return false;
        if (attrM[3] != null && attrM[3] !== "") return e.getAttribute(attrM[2]) === attrM[3];
        return true;
      }
      return e.localName === s.toLowerCase();
    }
  }

  // constants.ts
  var VOID_ELEMENTS = {
    area: true,
    base: true,
    br: true,
    col: true,
    embed: true,
    hr: true,
    img: true,
    input: true,
    link: true,
    meta: true,
    param: true,
    source: true,
    track: true,
    wbr: true
  };
  var RAWTEXT_ELEMENTS = {
    script: true,
    style: true
  };

  // html/serializer.ts
  function serializeChildren(node) {
    const cached = node.cachedInnerHTML;
    if (cached != null) return cached;
    let s = "";
    for (const c of node.childNodes) s += serializeNode(c);
    return s;
  }
  function serializeNode(node) {
    if (node.nodeType === 3 /* Text */) return escapeText(node.data);
    if (node.nodeType === 8 /* Comment */) return "<!--" + node.data + "-->";
    if (node.nodeType === 11 /* DocumentFragment */ || node.nodeType === 9 /* Document */) {
      return serializeChildren(node);
    }
    const el = node;
    const tag = el.localName;
    let s = "<" + tag;
    for (const k of el.getAttributeNames()) {
      s += " " + k + '="' + escapeAttr(el.getAttribute(k)) + '"';
    }
    if (!el.hasAttribute("style")) {
      const css = el.style?.cssText;
      if (css) s += ' style="' + escapeAttr(css) + '"';
    }
    s += ">";
    if (VOID_ELEMENTS[tag]) return s;
    s += serializeChildren(el);
    return s + "</" + tag + ">";
  }

  // dom/Element.ts
  var Element = class _Element extends Node {
    constructor(tag, ns) {
      super(1 /* Element */);
      this.attrs = /* @__PURE__ */ new Map();
      this.listeners = {};
      this.cachedInnerHTML = null;
      this._sheet = null;
      this.localName = String(tag).toLowerCase();
      this.tagName = this.localName.toUpperCase();
      this.nodeName = this.tagName;
      this.namespaceURI = ns || "http://www.w3.org/1999/xhtml";
      this.style = createStyleDeclaration();
    }
    setAttribute(name, value) {
      this.attrs.set(name, value == null ? "" : String(value));
    }
    setAttributeNS(_ns, name, value) {
      this.setAttribute(name, value);
    }
    getAttribute(name) {
      return this.attrs.has(name) ? this.attrs.get(name) : null;
    }
    removeAttribute(name) {
      this.attrs.delete(name);
    }
    removeAttributeNS(_ns, name) {
      this.attrs.delete(name);
    }
    hasAttribute(name) {
      return this.attrs.has(name);
    }
    getAttributeNames() {
      return Array.from(this.attrs.keys());
    }
    addEventListener(t, cb) {
      var _a;
      ((_a = this.listeners)[t] || (_a[t] = [])).push(cb);
    }
    removeEventListener(t, cb) {
      const list = this.listeners[t];
      if (!list) return;
      const i = list.indexOf(cb);
      if (i >= 0) list.splice(i, 1);
    }
    dispatchEvent(event) {
      const list = this.listeners[event.type];
      if (!list || !list.length) return true;
      event.target = this;
      event.currentTarget = this;
      const snapshot = list.slice();
      for (let i = 0; i < snapshot.length; i++) {
        try {
          snapshot[i](event);
        } catch {
        }
        if (event._stoppedImmediate) break;
      }
      event.currentTarget = null;
      return !event.defaultPrevented;
    }
    setAttributeNode() {
    }
    getElementsByTagName(tag) {
      const out = [];
      collectByTag(this, String(tag).toLowerCase(), out);
      return out;
    }
    querySelector(sel) {
      const r = querySelectorAll(this, sel);
      return r.length ? r[0] : null;
    }
    querySelectorAll(sel) {
      return querySelectorAll(this, sel);
    }
    closest() {
      return null;
    }
    getBoundingClientRect() {
      return { top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 };
    }
    contains(n) {
      let cur = n;
      while (cur) {
        if (cur === this) return true;
        cur = cur.parentNode;
      }
      return false;
    }
    get relList() {
      return {
        supports: () => true,
        add: () => {
        },
        remove: () => {
        },
        toggle: () => false,
        contains: () => false
      };
    }
    appendChild(child) {
      this.cachedInnerHTML = null;
      return super.appendChild(child);
    }
    insertBefore(child, ref) {
      this.cachedInnerHTML = null;
      const wasFrag = child.nodeType === 11 /* DocumentFragment */;
      const fragKids = wasFrag ? child.childNodes.slice() : null;
      const r = super.insertBefore(child, ref);
      if (this.isConnected) {
        if (fragKids) for (const k of fragKids) this._notifyConnected(k);
        else this._notifyConnected(child);
      }
      return r;
    }
    removeChild(child) {
      const wasConnected = child.isConnected;
      this.cachedInnerHTML = null;
      const r = super.removeChild(child);
      if (wasConnected) this._notifyDisconnected(child);
      return r;
    }
    get isConnected() {
      let n = this.parentNode;
      while (n) {
        if (n.nodeType === 9 /* Document */) return true;
        n = n.parentNode;
      }
      return false;
    }
    _notifyConnected(node) {
      if (node.nodeType === 1 /* Element */) {
        const el = node;
        if (!el._connected && typeof el.connectedCallback === "function" && el.isConnected) {
          el._connected = true;
          el.connectedCallback();
        }
      }
      const kids = node.childNodes;
      for (let i = 0; i < kids.length; i++) this._notifyConnected(kids[i]);
    }
    _notifyDisconnected(node) {
      if (node.nodeType === 1 /* Element */) {
        const el = node;
        if (el._connected && typeof el.disconnectedCallback === "function") {
          el._connected = false;
          el.disconnectedCallback();
        }
      }
      const kids = node.childNodes;
      for (let i = 0; i < kids.length; i++) this._notifyDisconnected(kids[i]);
    }
    get dataset() {
      const attrs = this.attrs;
      const key = (p) => "data-" + p.replace(/[A-Z]/g, (m) => "-" + m.toLowerCase());
      return new Proxy({}, {
        get(_t, p) {
          if (typeof p !== "string") return void 0;
          const v = attrs.get(key(p));
          return v == null ? void 0 : v;
        },
        set(_t, p, value) {
          if (typeof p === "string") attrs.set(key(p), String(value));
          return true;
        },
        has(_t, p) {
          return typeof p === "string" && attrs.has(key(p));
        }
      });
    }
    _shallowClone() {
      const el = new _Element(this.localName, this.namespaceURI);
      for (const [k, v] of this.attrs) el.attrs.set(k, v);
      return el;
    }
    get id() {
      return this.attrs.get("id") || "";
    }
    set id(v) {
      this.attrs.set("id", String(v));
    }
    get className() {
      return this.attrs.get("class") || "";
    }
    set className(v) {
      this.attrs.set("class", String(v));
    }
    get children() {
      return this.childNodes.filter((n) => n.nodeType === 1 /* Element */);
    }
    get innerHTML() {
      return this.cachedInnerHTML != null ? this.cachedInnerHTML : serializeChildren(this);
    }
    set innerHTML(v) {
      this.childNodes = [];
      this.cachedInnerHTML = v == null ? "" : String(v);
    }
    get textContent() {
      return textOf(this);
    }
    set textContent(v) {
      this.childNodes = [];
      this.cachedInnerHTML = null;
      if (v != null && v !== "") this.appendChild(new Text(v));
    }
    get outerHTML() {
      return serializeNode(this);
    }
    get sheet() {
      if (this.localName !== "style") return null;
      if (!this._sheet) {
        const rules = [];
        this._sheet = {
          cssRules: rules,
          rules,
          ownerNode: this,
          insertRule: (rule, index) => {
            const i = index == null ? rules.length : index;
            rules.splice(i, 0, { cssText: rule });
            return i;
          },
          deleteRule: (index) => {
            rules.splice(index, 1);
          }
        };
      }
      return this._sheet;
    }
  };

  // dom/Comment.ts
  var Comment = class _Comment extends Node {
    constructor(data) {
      super(8 /* Comment */);
      this.data = data == null ? "" : String(data);
    }
    _shallowClone() {
      return new _Comment(this.data);
    }
  };

  // dom/DocumentFragment.ts
  var DocumentFragment = class _DocumentFragment extends Node {
    constructor() {
      super(11 /* DocumentFragment */);
    }
    _shallowClone() {
      return new _DocumentFragment();
    }
  };

  // html/entities.ts
  var NAMED = {
    amp: "&",
    lt: "<",
    gt: ">",
    quot: '"',
    apos: "'",
    nbsp: " ",
    copy: "\xA9",
    reg: "\xAE",
    trade: "\u2122",
    hellip: "\u2026",
    mdash: "\u2014",
    ndash: "\u2013",
    lsquo: "\u2018",
    rsquo: "\u2019",
    ldquo: "\u201C",
    rdquo: "\u201D",
    laquo: "\xAB",
    raquo: "\xBB",
    deg: "\xB0",
    plusmn: "\xB1",
    times: "\xD7",
    divide: "\xF7",
    micro: "\xB5",
    euro: "\u20AC",
    pound: "\xA3",
    cent: "\xA2",
    yen: "\xA5",
    sect: "\xA7",
    para: "\xB6",
    middot: "\xB7",
    bull: "\u2022",
    frac12: "\xBD",
    frac14: "\xBC",
    frac34: "\xBE",
    sup2: "\xB2",
    sup3: "\xB3"
  };
  function decodeEntities(s) {
    if (s.indexOf("&") < 0) return s;
    return s.replace(/&#(x?[0-9a-fA-F]+);|&([a-zA-Z][a-zA-Z0-9]*);/g, (m, num, name) => {
      if (num != null) {
        const code = num.charAt(0) === "x" || num.charAt(0) === "X" ? parseInt(num.slice(1), 16) : parseInt(num, 10);
        return code > 0 && isFinite(code) ? String.fromCharCode(code) : m;
      }
      return Object.prototype.hasOwnProperty.call(NAMED, name) ? NAMED[name] : m;
    });
  }
  function indexOfCI(haystack, needle, from) {
    const n = needle.length, hl = haystack.length;
    for (let p = from; p <= hl - n; p++) {
      for (let q = 0; q < n; q++) {
        const c = haystack.charAt(p + q);
        if (c !== needle.charAt(q) && c.toLowerCase() !== needle.charAt(q)) break;
        if (q === n - 1) return p;
      }
    }
    return -1;
  }

  // html/tokenizer.ts
  function createTagScanners() {
    return {
      tagName: /[a-zA-Z][a-zA-Z0-9:_-]*/y,
      ws: /[\t\n\f\r ]+/y,
      attrName: /[^\t\n\f\r \/>"'<=]+/y,
      bareVal: /[^\t\n\f\r >]*/y
    };
  }
  function findRawTextClose(input, tag, from) {
    return indexOfCI(input, "</" + tag, from);
  }

  // html/parser.ts
  function parseHTML(doc2, input) {
    const src = input == null ? "" : String(input);
    const len = src.length;
    const sc = createTagScanners();
    const root = new Element("html");
    const head = new Element("head");
    const body = new Element("body");
    root.appendChild(head);
    root.appendChild(body);
    let open = [body];
    const cur = () => open[open.length - 1];
    function appendText(parent, text) {
      const last = parent.childNodes[parent.childNodes.length - 1];
      if (last && last.nodeType === 3 /* Text */) last.data += text;
      else parent.appendChild(new Text(text));
    }
    let i = 0;
    while (i < len) {
      const ch = src.charAt(i);
      if (ch !== "<") {
        let textEnd = src.indexOf("<", i);
        if (textEnd < 0) textEnd = len;
        appendText(cur(), decodeEntities(src.slice(i, textEnd)));
        i = textEnd;
        continue;
      }
      if (src.slice(i, i + 4) === "<!--") {
        const cEnd = src.indexOf("-->", i + 4);
        cur().appendChild(new Comment(src.slice(i + 4, cEnd < 0 ? len : cEnd)));
        i = cEnd < 0 ? len : cEnd + 3;
        continue;
      }
      if (src.charAt(i + 1) === "!" || src.charAt(i + 1) === "?") {
        const declEnd = src.indexOf(">", i);
        const end = declEnd < 0 ? len : declEnd;
        const bang = src.charAt(i + 1) === "!";
        const inner = src.slice(i + 2, end);
        if (!bang) cur().appendChild(new Comment("?" + inner));
        else if (!/^doctype/i.test(inner)) cur().appendChild(new Comment(inner));
        i = declEnd < 0 ? len : declEnd + 1;
        continue;
      }
      if (src.charAt(i + 1) === "/") {
        sc.tagName.lastIndex = i + 2;
        const tm = sc.tagName.exec(src);
        if (tm) {
          const closeName = tm[0].toLowerCase();
          for (let k = open.length - 1; k > 0; k--) {
            if (open[k].localName === closeName) {
              open.length = k;
              break;
            }
          }
        }
        const slashEnd = src.indexOf(">", i);
        i = slashEnd < 0 ? len : slashEnd + 1;
        continue;
      }
      sc.tagName.lastIndex = i + 1;
      const sm = sc.tagName.exec(src);
      if (!sm) {
        appendText(cur(), "<");
        i++;
        continue;
      }
      const tag = sm[0].toLowerCase();
      let j = sc.tagName.lastIndex;
      let attrs = null;
      let selfClosed = false;
      while (j < len) {
        sc.ws.lastIndex = j;
        if (sc.ws.exec(src)) j = sc.ws.lastIndex;
        if (j >= len) break;
        const atC = src.charAt(j);
        if (atC === ">") {
          j++;
          break;
        }
        if (atC === "/" && src.charAt(j + 1) === ">") {
          selfClosed = true;
          j += 2;
          break;
        }
        sc.attrName.lastIndex = j;
        const am = sc.attrName.exec(src);
        if (!am) {
          j++;
          continue;
        }
        const an = am[0].toLowerCase();
        j = sc.attrName.lastIndex;
        sc.ws.lastIndex = j;
        if (sc.ws.exec(src)) j = sc.ws.lastIndex;
        let val = "";
        if (src.charAt(j) === "=") {
          j++;
          sc.ws.lastIndex = j;
          if (sc.ws.exec(src)) j = sc.ws.lastIndex;
          const quote = src.charAt(j);
          if (quote === '"' || quote === "'") {
            const qEnd = src.indexOf(quote, j + 1);
            val = decodeEntities(qEnd < 0 ? src.slice(j + 1) : src.slice(j + 1, qEnd));
            j = qEnd < 0 ? len : qEnd + 1;
          } else {
            sc.bareVal.lastIndex = j;
            const bm = sc.bareVal.exec(src);
            val = decodeEntities(bm ? bm[0] : "");
            j = sc.bareVal.lastIndex;
          }
        }
        (attrs || (attrs = {}))[an] = val;
      }
      if (tag === "html") {
        if (attrs) for (const ha in attrs) root.setAttribute(ha, attrs[ha]);
        i = j;
        continue;
      }
      if (tag === "head") {
        open = [head];
        if (attrs) for (const he in attrs) head.setAttribute(he, attrs[he]);
        i = j;
        continue;
      }
      if (tag === "body") {
        open = [body];
        if (attrs) for (const bo in attrs) body.setAttribute(bo, attrs[bo]);
        i = j;
        continue;
      }
      const el = new Element(tag);
      if (attrs) for (const key in attrs) el.setAttribute(key, attrs[key]);
      if (RAWTEXT_ELEMENTS[tag]) {
        const rawFrom = j;
        const rawTo = findRawTextClose(src, tag, rawFrom);
        const raw = rawTo < 0 ? src.slice(rawFrom) : src.slice(rawFrom, rawTo);
        if (raw) el.appendChild(new Text(raw));
        const rawGt = rawTo < 0 ? len : src.indexOf(">", rawTo);
        i = rawGt < 0 ? len : rawGt + 1;
        cur().appendChild(el);
        continue;
      }
      cur().appendChild(el);
      if (!VOID_ELEMENTS[tag] && !selfClosed) open.push(el);
      i = j;
    }
    doc2.documentElement = root;
    doc2.head = head;
    doc2.body = body;
    root.parentNode = doc2;
    doc2.childNodes = [root];
    return root;
  }
  function parseFragment(html) {
    const scratch = {};
    parseHTML(scratch, html);
    const kids = scratch.body.childNodes.slice();
    for (const k of kids) k.parentNode = null;
    return kids;
  }

  // dom/HTMLTemplateElement.ts
  var HTMLTemplateElement = class _HTMLTemplateElement extends Element {
    constructor() {
      super("template");
      this.content = new DocumentFragment();
    }
    get innerHTML() {
      return serializeChildren(this.content);
    }
    set innerHTML(v) {
      this.content.childNodes = [];
      for (const k of parseFragment(v)) this.content.appendChild(k);
    }
    _shallowClone() {
      const clone = new _HTMLTemplateElement();
      for (const c of this.content.childNodes) clone.content.appendChild(c.cloneNode(true));
      return clone;
    }
  };

  // dom/customElements.ts
  var CustomElementRegistry = class {
    constructor() {
      this._definitions = /* @__PURE__ */ new Map();
      this._pending = /* @__PURE__ */ new Map();
      this._nameStack = [];
      this._upgradeTarget = null;
      this._doc = null;
    }
    setDocument(doc2) {
      this._doc = doc2;
    }
    define(name, ctor, options) {
      const tag = String(name).toLowerCase();
      if (this._definitions.has(tag)) return;
      const extendsTag = options && options.extends ? String(options.extends).toLowerCase() : null;
      this._definitions.set(tag, { ctor, extendsTag });
      if (this._doc) this._upgradeSubtree(this._doc.documentElement);
      const waiters = this._pending.get(tag);
      if (waiters) {
        this._pending.delete(tag);
        for (const w of waiters) w(ctor);
      }
    }
    get(name) {
      const def = this._definitions.get(String(name).toLowerCase());
      return def ? def.ctor : void 0;
    }
    whenDefined(name) {
      const tag = String(name).toLowerCase();
      const def = this._definitions.get(tag);
      if (def) return Promise.resolve(def.ctor);
      return new Promise((resolve) => {
        const arr = this._pending.get(tag) || [];
        arr.push(resolve);
        this._pending.set(tag, arr);
      });
    }
    upgrade(root) {
      if (root) this._upgradeSubtree(root);
    }
    // createElement path: construct a fresh instance with the registry-supplied tag on the name stack so a
    // subclass `super()` lands on HTMLElement with the right localName. null for unregistered names.
    tryCreate(name) {
      const def = this._definitions.get(name);
      if (!def) return null;
      this._nameStack.push(name);
      try {
        return new def.ctor();
      } finally {
        this._nameStack.pop();
      }
    }
    currentName() {
      return this._nameStack[this._nameStack.length - 1];
    }
    takeUpgradeTarget() {
      const t = this._upgradeTarget;
      this._upgradeTarget = null;
      return t;
    }
    _upgradeSubtree(root) {
      const stack = [root];
      while (stack.length) {
        const n = stack.pop();
        if (!n) continue;
        if (n.nodeType === 1 /* Element */) {
          const def = this._definitions.get(n.localName);
          if (def) this._upgradeOne(n, def.ctor);
        }
        const kids = n.childNodes;
        if (kids) for (let i = 0; i < kids.length; i++) stack.push(kids[i]);
      }
    }
    _upgradeOne(el, ctor) {
      if (!ctor || el instanceof ctor) return;
      Object.setPrototypeOf(el, ctor.prototype);
      this._upgradeTarget = el;
      try {
        new ctor();
      } catch {
      } finally {
        this._upgradeTarget = null;
      }
      if (typeof el.connectedCallback === "function" && el.isConnected) {
        el._connected = true;
        el.connectedCallback();
      }
    }
  };
  var customElements = new CustomElementRegistry();

  // dom/Document.ts
  var Document = class _Document extends Node {
    constructor(defaultView) {
      super(9 /* Document */);
      this.documentElement = null;
      this.head = null;
      this.body = null;
      this.styleSheets = [];
      this.defaultView = defaultView || null;
    }
    createElement(tag) {
      const name = String(tag).toLowerCase();
      if (name === "template") return new HTMLTemplateElement();
      const custom = customElements.tryCreate(name);
      return custom || new Element(name);
    }
    createElementNS(ns, tag) {
      return new Element(tag, ns);
    }
    createTextNode(data) {
      return new Text(data);
    }
    createComment(data) {
      return new Comment(data);
    }
    createDocumentFragment() {
      return new DocumentFragment();
    }
    getElementById(id) {
      return walkFind(this.documentElement, (e) => e.getAttribute("id") === id);
    }
    getElementsByTagName(tag) {
      const out = [];
      if (this.documentElement) collectByTag(this.documentElement, String(tag).toLowerCase(), out);
      return out;
    }
    querySelector(sel) {
      const r = querySelectorAll(this, sel);
      return r.length ? r[0] : null;
    }
    querySelectorAll(sel) {
      return querySelectorAll(this, sel);
    }
    addEventListener() {
    }
    removeEventListener() {
    }
    createEvent() {
      return { initEvent() {
      } };
    }
    get ownerDocument() {
      return null;
    }
    _shallowClone() {
      return new _Document(this.defaultView);
    }
  };

  // dom/HTMLElement.ts
  var HTMLElement = class extends Element {
    constructor(tag, ns) {
      super(tag || customElements.currentName() || "", ns);
      this.shadowRoot = null;
      const target = customElements.takeUpgradeTarget();
      if (target) return target;
    }
    attachShadow(init) {
      if (this.shadowRoot) return this.shadowRoot;
      const root = new DocumentFragment();
      root.host = this;
      root.mode = init && init.mode ? init.mode : "open";
      this.shadowRoot = root;
      return root;
    }
    connectedCallback() {
    }
    disconnectedCallback() {
    }
    adoptedCallback() {
    }
    attributeChangedCallback(_name, _oldValue, _newValue) {
    }
    setAttribute(name, value) {
      const observed = this.constructor.observedAttributes;
      const tracked = Array.isArray(observed) && observed.indexOf(name) >= 0;
      const old = tracked ? this.getAttribute(name) : null;
      super.setAttribute(name, value);
      if (tracked && typeof this.attributeChangedCallback === "function") {
        this.attributeChangedCallback(name, old, this.getAttribute(name));
      }
    }
  };

  // dom/htmlInterfaces.ts
  var htmlInterfaces_exports = {};
  __export(htmlInterfaces_exports, {
    HTMLAnchorElement: () => HTMLAnchorElement,
    HTMLButtonElement: () => HTMLButtonElement,
    HTMLCanvasElement: () => HTMLCanvasElement,
    HTMLFormElement: () => HTMLFormElement,
    HTMLIFrameElement: () => HTMLIFrameElement,
    HTMLImageElement: () => HTMLImageElement,
    HTMLInputElement: () => HTMLInputElement,
    HTMLLinkElement: () => HTMLLinkElement,
    HTMLOptionElement: () => HTMLOptionElement,
    HTMLScriptElement: () => HTMLScriptElement,
    HTMLSelectElement: () => HTMLSelectElement,
    HTMLStyleElement: () => HTMLStyleElement,
    HTMLTextAreaElement: () => HTMLTextAreaElement,
    HTMLUnknownElement: () => HTMLUnknownElement,
    MathMLElement: () => MathMLElement,
    SVGElement: () => SVGElement,
    SVGSVGElement: () => SVGSVGElement
  });
  var HTMLIFrameElement = class extends HTMLElement {
  };
  var HTMLInputElement = class extends HTMLElement {
  };
  var HTMLTextAreaElement = class extends HTMLElement {
  };
  var HTMLSelectElement = class extends HTMLElement {
  };
  var HTMLOptionElement = class extends HTMLElement {
  };
  var HTMLButtonElement = class extends HTMLElement {
  };
  var HTMLAnchorElement = class extends HTMLElement {
  };
  var HTMLImageElement = class extends HTMLElement {
  };
  var HTMLFormElement = class extends HTMLElement {
  };
  var HTMLStyleElement = class extends HTMLElement {
  };
  var HTMLScriptElement = class extends HTMLElement {
  };
  var HTMLLinkElement = class extends HTMLElement {
  };
  var HTMLCanvasElement = class extends HTMLElement {
  };
  var HTMLUnknownElement = class extends HTMLElement {
  };
  var SVGElement = class extends Element {
  };
  var SVGSVGElement = class extends SVGElement {
  };
  var MathMLElement = class extends Element {
  };

  // browser/navigator.ts
  var navigator = {
    userAgent: "SimpleCrawler",
    platform: "",
    language: "en"
  };

  // browser/location.ts
  function createLocation() {
    return {
      href: "http://localhost/",
      protocol: "http:",
      host: "localhost",
      hostname: "localhost",
      port: "",
      pathname: "/",
      search: "",
      hash: "",
      origin: "http://localhost"
    };
  }

  // url/resolve.ts
  function currentLocation() {
    return globalThis.location;
  }
  function resolveUrl(u, base) {
    const input = String(u ?? "");
    if (/^[a-zA-Z][\w+.-]*:\/\//.test(input)) return input;
    const b = String(base || currentLocation()?.href || "http://localhost/");
    const bm = b.match(/^([a-zA-Z][\w+.-]*:\/\/[^/?#]*)([^?#]*)/) || [];
    const origin = bm[1] || "http://localhost";
    if (input.charAt(0) === "/") return origin + input;
    if (input.charAt(0) === "#" || input.charAt(0) === "?") return origin + (bm[2] || "/") + input;
    const dir = (bm[2] || "/").replace(/[^/]*$/, "");
    return origin + dir + input;
  }
  function applyUrl(u) {
    try {
      let abs = u;
      if (u.indexOf("http") !== 0) {
        const base = currentLocation()?.origin || "http://localhost";
        abs = u.charAt(0) === "/" ? base + u : base + "/" + u;
      }
      const m = abs.match(/^(https?:)\/\/([^/?#]+)([^?#]*)(\?[^#]*)?(#.*)?$/);
      if (!m) return;
      const loc = currentLocation();
      if (!loc) return;
      loc.href = abs;
      loc.protocol = m[1];
      loc.host = m[2];
      loc.hostname = m[2].split(":")[0];
      loc.port = m[2].split(":")[1] || "";
      loc.pathname = m[3] || "/";
      loc.search = m[4] || "";
      loc.hash = m[5] || "";
      loc.origin = m[1] + "//" + m[2];
    } catch {
    }
  }

  // browser/history.ts
  function createHistory() {
    return {
      pushState: (_s, _t, u) => {
        if (u) applyUrl(u);
      },
      replaceState: (_s, _t, u) => {
        if (u) applyUrl(u);
      },
      go: () => {
      },
      back: () => {
      },
      forward: () => {
      },
      length: 1,
      state: null
    };
  }

  // scheduler/taskQueue.ts
  var tasks = [];
  function enqueue(cb) {
    if (typeof cb === "function") tasks.push(cb);
    return tasks.length;
  }
  function pendingCount() {
    return tasks.length;
  }
  function pumpTasks() {
    if (!tasks.length) return 0;
    const batch = tasks.splice(0, tasks.length);
    for (const fn of batch) {
      try {
        fn();
      } catch {
      }
    }
    return tasks.length;
  }
  function installTimerGlobals(global) {
    global.queueMicrotask = (cb) => enqueue(cb);
    global.setTimeout = (cb) => enqueue(cb);
    global.clearTimeout = () => {
    };
    global.setInterval = () => 0;
    global.clearInterval = () => {
    };
    global.requestAnimationFrame = (cb) => enqueue(() => cb(0));
    global.cancelAnimationFrame = () => {
    };
  }

  // url/URLSearchParams.ts
  var URLSearchParams = class {
    constructor(init) {
      this.pairs = [];
      let src = init;
      if (typeof src === "string" && src.charAt(0) === "?") src = src.slice(1);
      if (typeof src === "string" && src) {
        src.split("&").forEach((p) => {
          if (!p) return;
          const i = p.indexOf("=");
          this.pairs.push(i < 0 ? [decodeURIComponent(p), ""] : [decodeURIComponent(p.slice(0, i)), decodeURIComponent(p.slice(i + 1))]);
        });
      }
    }
    get(k) {
      for (const pair of this.pairs) if (pair[0] === k) return pair[1];
      return null;
    }
    getAll(k) {
      return this.pairs.filter((p) => p[0] === k).map((p) => p[1]);
    }
    has(k) {
      return this.get(k) !== null;
    }
    set(k, v) {
      this.delete(k);
      this.pairs.push([k, String(v)]);
    }
    append(k, v) {
      this.pairs.push([k, String(v)]);
    }
    delete(k) {
      this.pairs = this.pairs.filter((p) => p[0] !== k);
    }
    forEach(cb) {
      this.pairs.forEach((p) => cb(p[1], p[0]));
    }
    entries() {
      let i = 0;
      const it = {
        next: () => i < this.pairs.length ? { value: this.pairs[i++], done: false } : { value: void 0, done: true },
        [Symbol.iterator]() {
          return this;
        }
      };
      return it;
    }
    keys() {
      return this.pairs.map((p) => p[0])[Symbol.iterator]();
    }
    values() {
      return this.pairs.map((p) => p[1])[Symbol.iterator]();
    }
    toString() {
      return this.pairs.map((p) => encodeURIComponent(p[0]) + "=" + encodeURIComponent(p[1])).join("&");
    }
    [Symbol.iterator]() {
      return this.entries();
    }
  };

  // url/Url.ts
  var URL = class {
    constructor(url, base) {
      const abs = resolveUrl(url, base);
      const m = abs.match(/^([a-zA-Z][\w+.-]*:)\/\/([^/?#]*)([^?#]*)(\?[^#]*)?(#.*)?$/) || [];
      this.href = abs;
      this.protocol = m[1] || "";
      this.host = m[2] || "";
      this.hostname = (m[2] || "").split(":")[0];
      this.port = (m[2] || "").split(":")[1] || "";
      this.pathname = m[3] || "/";
      this.search = m[4] || "";
      this.hash = m[5] || "";
      this.origin = this.protocol + "//" + this.host;
      this.searchParams = new URLSearchParams(this.search);
    }
    toString() {
      return this.href;
    }
  };

  // browser/Event.ts
  var Event = class {
    constructor(type, init) {
      this.isTrusted = false;
      this.defaultPrevented = false;
      this.eventPhase = 0;
      this.target = null;
      this.currentTarget = null;
      this._stoppedImmediate = false;
      this.type = String(type);
      this.bubbles = !!(init && init.bubbles);
      this.cancelable = !!(init && init.cancelable);
      this.timeStamp = Date.now();
    }
    preventDefault() {
      if (this.cancelable) this.defaultPrevented = true;
    }
    stopPropagation() {
    }
    stopImmediatePropagation() {
      this._stoppedImmediate = true;
    }
  };

  // browser/CustomEvent.ts
  var CustomEvent = class extends Event {
    constructor(type, init) {
      super(type, init);
      this.detail = init && init.detail !== void 0 ? init.detail : null;
    }
  };

  // browser/TextEncoder.ts
  var TextEncoder = class {
    constructor() {
      this.encoding = "utf-8";
    }
    encode(input) {
      const s = input == null ? "" : String(input);
      const out = [];
      for (let i = 0; i < s.length; ) {
        const c = s.charCodeAt(i++);
        if (c < 128) {
          out.push(c);
        } else if (c < 2048) {
          out.push(192 | c >> 6, 128 | c & 63);
        } else if (c >= 55296 && c <= 56319 && i < s.length) {
          const c2 = s.charCodeAt(i++);
          const cp = 65536 + ((c & 1023) << 10) + (c2 & 1023);
          out.push(240 | cp >> 18, 128 | cp >> 12 & 63, 128 | cp >> 6 & 63, 128 | cp & 63);
        } else {
          out.push(224 | c >> 12, 128 | c >> 6 & 63, 128 | c & 63);
        }
      }
      return new Uint8Array(out);
    }
  };

  // browser/TextDecoder.ts
  var TextDecoder = class {
    constructor() {
      this.encoding = "utf-8";
    }
    decode(input) {
      if (input == null) return "";
      if (typeof input === "string") return input;
      const bytes = input;
      let out = "";
      let i = 0;
      const len = bytes.length;
      while (i < len) {
        const b1 = bytes[i++];
        if (b1 < 128) {
          out += String.fromCharCode(b1);
        } else if (b1 < 224) {
          const b2 = bytes[i++];
          out += String.fromCharCode((b1 & 31) << 6 | b2 & 63);
        } else if (b1 < 240) {
          const b2 = bytes[i++];
          const b3 = bytes[i++];
          out += String.fromCharCode((b1 & 15) << 12 | (b2 & 63) << 6 | b3 & 63);
        } else {
          const b2 = bytes[i++];
          const b3 = bytes[i++];
          const b4 = bytes[i++];
          const cp = (b1 & 7) << 18 | (b2 & 63) << 12 | (b3 & 63) << 6 | b4 & 63;
          const adj = cp - 65536;
          out += String.fromCharCode(55296 | adj >> 10, 56320 | adj & 63);
        }
      }
      return out;
    }
  };

  // browser/crypto.ts
  function randomByte() {
    return Math.floor(Math.random() * 256);
  }
  function hex(n) {
    return n < 16 ? "0" + n.toString(16) : n.toString(16);
  }
  var crypto = {
    getRandomValues(arr) {
      if (arr) for (let i = 0; i < arr.length; i++) arr[i] = randomByte();
      return arr;
    },
    randomUUID() {
      const b = new Uint8Array(16);
      for (let i = 0; i < 16; i++) b[i] = randomByte();
      b[6] = b[6] & 15 | 64;
      b[8] = b[8] & 63 | 128;
      const h = [];
      for (let i = 0; i < 16; i++) h.push(hex(b[i]));
      return h[0] + h[1] + h[2] + h[3] + "-" + h[4] + h[5] + "-" + h[6] + h[7] + "-" + h[8] + h[9] + "-" + h[10] + h[11] + h[12] + h[13] + h[14] + h[15];
    }
  };

  // browser/MessageChannel.ts
  var MessagePort = class {
    constructor() {
      this.onmessage = null;
      this.other = null;
    }
    postMessage(data) {
      const o = this.other;
      if (o) enqueue(() => {
        if (o.onmessage) o.onmessage({ data });
      });
    }
    start() {
    }
    close() {
    }
    addEventListener(type, cb) {
      if (type === "message") this.onmessage = cb;
    }
    removeEventListener(type, cb) {
      if (type === "message" && this.onmessage === cb) this.onmessage = null;
    }
    _link(other) {
      this.other = other;
    }
  };
  var MessageChannel = class {
    constructor() {
      this.port1 = new MessagePort();
      this.port2 = new MessagePort();
      this.port1._link(this.port2);
      this.port2._link(this.port1);
    }
  };

  // browser/Storage.ts
  var Storage = class {
    constructor() {
      this.store = /* @__PURE__ */ new Map();
    }
    get length() {
      return this.store.size;
    }
    getItem(key) {
      return this.store.has(key) ? this.store.get(key) : null;
    }
    setItem(key, value) {
      this.store.set(String(key), value == null ? "" : String(value));
    }
    removeItem(key) {
      this.store.delete(String(key));
    }
    clear() {
      this.store.clear();
    }
    key(index) {
      if (index < 0 || index >= this.store.size) return null;
      let i = 0;
      for (const k of this.store.keys()) {
        if (i++ === index) return k;
      }
      return null;
    }
  };
  function createStorage() {
    return new Storage();
  }

  // browser/Performance.ts
  var startTime = Date.now();
  var Performance = class {
    constructor() {
      this.timeOrigin = startTime;
    }
    now() {
      return Date.now() - startTime;
    }
    mark() {
      return null;
    }
    measure() {
      return null;
    }
    clearMarks() {
    }
    clearMeasures() {
    }
    getEntries() {
      return [];
    }
    getEntriesByName() {
      return [];
    }
    getEntriesByType() {
      return [];
    }
  };
  var performance = new Performance();

  // browser/globals.ts
  var doc = new Document(globalThis);
  documentRef.current = doc;
  function installDOM(global) {
    global.document = doc;
    global.window = global;
    global.self = global;
    global.navigator = navigator;
    global.console = global.console || {
      log() {
      },
      warn() {
      },
      error() {
      },
      info() {
      },
      debug() {
      }
    };
    global.location = createLocation();
    global.history = createHistory();
    global.addEventListener = () => {
    };
    global.removeEventListener = () => {
    };
    global.dispatchEvent = () => true;
    global.matchMedia = () => ({
      matches: false,
      addListener() {
      },
      removeListener() {
      },
      addEventListener() {
      },
      removeEventListener() {
      }
    });
    global.getComputedStyle = () => ({ getPropertyValue: () => "" });
    global.MutationObserver = function() {
      this.observe = () => {
      };
      this.disconnect = () => {
      };
      this.takeRecords = () => [];
    };
    global.URL = URL;
    global.URLSearchParams = URLSearchParams;
    global.Node = Node;
    global.Element = Element;
    global.Document = Document;
    global.Text = Text;
    global.Comment = Comment;
    global.DocumentFragment = DocumentFragment;
    global.HTMLElement = HTMLElement;
    global.HTMLTemplateElement = HTMLTemplateElement;
    for (const name in htmlInterfaces_exports) global[name] = htmlInterfaces_exports[name];
    global.customElements = customElements;
    customElements.setDocument(doc);
    global.Event = Event;
    global.CustomEvent = CustomEvent;
    global.TextEncoder = global.TextEncoder || TextEncoder;
    global.TextDecoder = global.TextDecoder || TextDecoder;
    global.crypto = global.crypto || crypto;
    global.MessageChannel = global.MessageChannel || MessageChannel;
    global.MessagePort = global.MessagePort || MessagePort;
    global.performance = global.performance || performance;
    global.localStorage = createStorage();
    global.sessionStorage = createStorage();
    installTimerGlobals(global);
  }

  // crawler/api.ts
  function collectScripts() {
    const out = [];
    if (!doc.documentElement) return out;
    function walk(n) {
      for (const c of n.childNodes) {
        if (c.nodeType !== 1 /* Element */) continue;
        if (c.localName === "script") {
          const type = c.getAttribute("type") || "";
          if (type && type !== "text/javascript" && type !== "module" && type !== "application/javascript") {
            walk(c);
            continue;
          }
          out.push({
            module: type === "module",
            external: !!c.getAttribute("src"),
            src: c.getAttribute("src") || "",
            text: c.textContent
          });
        }
        walk(c);
      }
    }
    walk(doc.documentElement);
    return out;
  }
  function collectLinks() {
    const anchors = [];
    let canonical = null;
    let robots = null;
    if (!doc.documentElement) return { anchors, canonical, robots };
    function walk(n) {
      for (const c of n.childNodes) {
        if (c.nodeType !== 1 /* Element */) continue;
        const tag = c.localName;
        if (tag === "a") {
          anchors.push(c.getAttribute("href"));
        } else if (canonical == null && tag === "link") {
          const rel = (c.getAttribute("rel") || "").toLowerCase().split(/\s+/);
          if (rel.indexOf("canonical") >= 0) canonical = c.getAttribute("href");
        } else if (robots == null && tag === "meta") {
          if ((c.getAttribute("name") || "").toLowerCase() === "robots") robots = c.getAttribute("content");
        }
        walk(c);
      }
    }
    walk(doc.documentElement);
    return { anchors, canonical, robots };
  }
  function installCrawlerApi(global) {
    global.__crawlerSetLocation = (url) => {
      applyUrl(url);
    };
    global.__crawlerLoadHtml = (html) => {
      parseHTML(doc, html);
    };
    global.__crawlerCollectScripts = () => JSON.stringify(collectScripts());
    global.__crawlerCollectLinks = () => JSON.stringify(collectLinks());
    global.__crawlerPending = () => pendingCount();
    global.__crawlerPump = () => pumpTasks();
    global.__crawlerSerialize = () => doc.documentElement ? serializeNode(doc.documentElement) : "";
  }

  // index.ts
  installDOM(globalThis);
  installCrawlerApi(globalThis);
})();
