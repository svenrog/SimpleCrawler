"use strict";
(() => {
  var __defProp = Object.defineProperty;
  var __export = (target, all) => {
    for (var name in all)
      __defProp(target, name, { get: all[name], enumerable: true });
  };

  // dom/documentRef.ts
  var documentRef = { current: null };

  // dom/utils.ts
  function hideOwnFields(node) {
    const keys = Object.keys(node);
    for (let i = 0; i < keys.length; i++) {
      Object.defineProperty(node, keys[i], { enumerable: false });
    }
  }
  function escapeAttr(v) {
    return String(v).replace(/&/g, "&amp;").replace(/"/g, "&quot;");
  }
  function escapeText(v) {
    return String(v).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }
  function collectByTag(node, tag, out) {
    const all = tag === "*";
    const children = node.childNodes;
    for (let i = 0; i < children.length; i++) {
      const c = children[i];
      if (c.nodeType === 1 /* Element */) {
        if (all || c.localName === tag) out.push(c);
        collectByTag(c, tag, out);
      }
    }
  }
  function collectByPredicate(node, pred, out) {
    const children = node.childNodes;
    for (let i = 0; i < children.length; i++) {
      const c = children[i];
      if (c.nodeType === 1 /* Element */) {
        if (pred(c)) out.push(c);
        collectByPredicate(c, pred, out);
      }
    }
  }
  function collectByClass(node, className, out) {
    const children = node.childNodes;
    for (let i = 0; i < children.length; i++) {
      const c = children[i];
      if (c.nodeType === 1 /* Element */) {
        if (c.classList.contains(className)) out.push(c);
        collectByClass(c, className, out);
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

  // dom/eventTarget.ts
  function addListener(map, type, cb) {
    (map[type] || (map[type] = [])).push(cb);
  }
  function removeListener(map, type, cb) {
    const list = map[type];
    if (!list) return;
    const i = list.indexOf(cb);
    if (i >= 0) list.splice(i, 1);
  }
  function fireEvent(target, map, event) {
    const list = map[event.type];
    if (!list || !list.length) return true;
    event.target = target;
    event.currentTarget = target;
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
  var EventTarget = class {
    constructor() {
      this._listeners = null;
    }
    addEventListener(type, cb) {
      addListener(this._listeners || (this._listeners = {}), type, cb);
    }
    removeEventListener(type, cb) {
      if (this._listeners) removeListener(this._listeners, type, cb);
    }
    dispatchEvent(event) {
      return this._listeners ? fireEvent(this, this._listeners, event) : true;
    }
  };

  // dom/Node.ts
  var Node = class extends EventTarget {
    constructor(type) {
      super();
      this.parentNode = null;
      this.childNodes = [];
      this.nodeType = type;
      hideOwnFields(this);
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
    hasChildNodes() {
      return this.childNodes.length > 0;
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
    isEqualNode(other) {
      if (!other || other.nodeType !== this.nodeType) return false;
      const a = this;
      const b = other;
      if (this.nodeType === 1 /* Element */) {
        if (a.nodeName !== b.nodeName || a.namespaceURI !== b.namespaceURI) return false;
        const names = a.getAttributeNames();
        if (names.length !== b.getAttributeNames().length) return false;
        for (const name of names) if (a.getAttribute(name) !== b.getAttribute(name)) return false;
      } else if (this.nodeType === 3 /* Text */ || this.nodeType === 8 /* Comment */) {
        if (a.nodeValue !== b.nodeValue) return false;
      }
      if (this.childNodes.length !== other.childNodes.length) return false;
      for (let i = 0; i < this.childNodes.length; i++)
        if (!this.childNodes[i].isEqualNode(other.childNodes[i])) return false;
      return true;
    }
    isSameNode(other) {
      return other === this;
    }
    // React's getHoistableRoot falls back to `container.getRootNode?.() ?? container.ownerDocument` when the
    // container is the document itself (whose ownerDocument is null) — without this, that lookup throws.
    getRootNode() {
      let n = this;
      while (n.parentNode) n = n.parentNode;
      return n;
    }
  };
  function asNode(value) {
    return value instanceof Node ? value : documentRef.current.createTextNode(value);
  }

  // dom/CharacterData.ts
  var CharacterData = class extends Node {
    constructor(type, data) {
      super(type);
      this.data = data == null ? "" : String(data);
    }
    get nodeValue() {
      return this.data;
    }
    set nodeValue(v) {
      this.data = v == null ? "" : String(v);
    }
  };

  // dom/Text.ts
  var Text = class _Text extends CharacterData {
    constructor(data) {
      super(3 /* Text */, data);
      hideOwnFields(this);
    }
    get nodeName() {
      return "#text";
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

  // dom/NodeList.ts
  var NodeList = class extends Array {
    item(index) {
      return this[index] ?? null;
    }
  };

  // selector/querySelector.ts
  function querySelectorAll(root, sel) {
    const el = root.documentElement || root;
    const out = new NodeList();
    const s = String(sel).trim();
    walk(el);
    return out;
    function walk(n) {
      if (n.nodeType === 1 /* Element */ && matchesSelector(n, s)) out.push(n);
      for (const c of n.childNodes) walk(c);
    }
  }
  function matchesSelector(el, selector) {
    const s = String(selector).trim();
    if (!s) return false;
    for (const part of s.split(",")) {
      const compound = rightmostCompound(part);
      if (compound && matchesCompound(el, compound)) return true;
    }
    return false;
  }
  function rightmostCompound(part) {
    const s = part.trim();
    let start = 0;
    let depth = 0;
    let quote = "";
    for (let i = 0; i < s.length; i++) {
      const ch = s[i];
      if (quote) {
        if (ch === quote) quote = "";
        continue;
      }
      if (ch === '"' || ch === "'") quote = ch;
      else if (ch === "[") depth++;
      else if (ch === "]") {
        if (depth > 0) depth--;
      } else if (depth === 0 && (ch === ">" || ch === "+" || ch === "~" || /\s/.test(ch))) start = i + 1;
    }
    return s.slice(start);
  }
  function matchesCompound(el, compound) {
    const re = /[#.]?[\w-]+|\[[^\]]*\]|\*/g;
    let m;
    let matched = 0;
    while (m = re.exec(compound)) {
      matched++;
      const tok = m[0];
      const c = tok[0];
      if (tok === "*") continue;
      if (c === "#") {
        if (el.getAttribute("id") !== tok.slice(1)) return false;
      } else if (c === ".") {
        if (!hasClass(el, tok.slice(1))) return false;
      } else if (c === "[") {
        if (!matchesAttr(el, tok)) return false;
      } else if (el.localName !== tok.toLowerCase()) {
        return false;
      }
    }
    return matched > 0;
  }
  function hasClass(el, name) {
    const cls = el.getAttribute("class");
    if (!cls) return false;
    return cls.split(/\s+/).indexOf(name) >= 0;
  }
  function matchesAttr(el, token) {
    const m = token.match(/^\[([\w-]+)(?:([~|^$*]?=)["']?([^"'\]]*)["']?)?\]$/);
    if (!m) return false;
    const name = m[1];
    if (!el.hasAttribute(name)) return false;
    const op = m[2];
    if (!op) return true;
    const expected = m[3] ?? "";
    const actual = el.getAttribute(name) ?? "";
    switch (op) {
      case "=":
        return actual === expected;
      case "~=":
        return actual.split(/\s+/).indexOf(expected) >= 0;
      case "|=":
        return actual === expected || actual.startsWith(expected + "-");
      case "^=":
        return expected !== "" && actual.startsWith(expected);
      case "$=":
        return expected !== "" && actual.endsWith(expected);
      case "*=":
        return expected !== "" && actual.indexOf(expected) >= 0;
      default:
        return true;
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

  // html/parserRef.ts
  var parserRef = { parseFragment: null };

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

  // diagnostics.ts
  function reportSwallowed(context, error) {
    try {
      const report = globalThis.__crawlerDiagnostic;
      if (typeof report !== "function") return;
      report("swallowed exception in " + context + ": " + describeError(error));
    } catch {
    }
  }
  function describeError(error) {
    if (!(error instanceof Error)) return String(error);
    const message = error.message || String(error);
    const stack = error.stack;
    if (!stack) return message;
    return stack.indexOf(message) >= 0 ? stack : message + "\n" + stack;
  }

  // dom/resourceLoader.ts
  var _counter = 0;
  var _pending = [];
  var _byId = /* @__PURE__ */ new Map();
  var _seen = /* @__PURE__ */ new WeakSet();
  function registerResource(node) {
    const tag = node.localName;
    if (tag !== "script" && tag !== "link") return;
    if (tag === "script" && !node.getAttribute("src")) return;
    if (_seen.has(node)) return;
    _seen.add(node);
    const id = ++_counter;
    _pending.push({ id, node });
    _byId.set(id, node);
  }
  function takeResources() {
    if (!_pending.length) return "";
    const batch = _pending.splice(0, _pending.length);
    return JSON.stringify(batch.map((r) => ({ id: r.id, tag: r.node.localName, src: r.node.getAttribute("src") || "" })));
  }
  function pendingResourceCount() {
    return _pending.length;
  }
  function fireResourceEvent(id, type) {
    const node = _byId.get(id);
    if (!node) return;
    _byId.delete(id);
    const event = new Event(type);
    event.target = node;
    const handler = type === "load" ? node.onload : node.onerror;
    if (typeof handler === "function") {
      try {
        handler.call(node, event);
      } catch (e) {
        reportSwallowed("resource " + type + " handler", e);
      }
    }
    if (typeof node.dispatchEvent === "function") {
      try {
        node.dispatchEvent(event);
      } catch (e) {
        reportSwallowed("resource " + type + " dispatch", e);
      }
    }
  }

  // browser/viewport.ts
  var _width = 1920;
  var _height = 1080;
  function setViewport(width, height) {
    const w = Number(width);
    const h = Number(height);
    if (w > 0) _width = Math.floor(w);
    if (h > 0) _height = Math.floor(h);
  }
  function viewportWidth() {
    return _width;
  }
  function viewportHeight() {
    return _height;
  }
  function numeric(value) {
    const m = /-?\d*\.?\d+/.exec(value);
    return m ? parseFloat(m[0]) : NaN;
  }
  function resolutionDppx(value) {
    const n = numeric(value);
    if (isNaN(n)) return NaN;
    if (/dpi/i.test(value)) return n / 96;
    if (/dpcm/i.test(value)) return n / 37.795;
    return n;
  }
  function matchFeature(name, value) {
    switch (name) {
      case "min-width":
      case "min-device-width":
        return _width >= numeric(value);
      case "max-width":
      case "max-device-width":
        return _width <= numeric(value);
      case "width":
      case "device-width":
        return _width === numeric(value);
      case "min-height":
      case "min-device-height":
        return _height >= numeric(value);
      case "max-height":
      case "max-device-height":
        return _height <= numeric(value);
      case "height":
      case "device-height":
        return _height === numeric(value);
      case "min-resolution":
        return 1 >= resolutionDppx(value);
      case "max-resolution":
        return 1 <= resolutionDppx(value);
      case "resolution":
        return resolutionDppx(value) === 1;
      case "orientation":
        return value === "portrait" ? _height > _width : _width >= _height;
      // An unmodelled feature must never veto a layout (the crawl must not hide content), so it matches.
      default:
        return true;
    }
  }
  function matchClause(clause) {
    const inner = clause.replace(/^\(/, "").replace(/\)$/, "");
    const colon = inner.indexOf(":");
    if (colon < 0) return true;
    const name = inner.slice(0, colon).trim().toLowerCase();
    const value = inner.slice(colon + 1).trim().toLowerCase();
    return matchFeature(name, value);
  }
  function matchSingle(query) {
    let q = query.trim().toLowerCase();
    if (!q) return true;
    let negate = false;
    if (q.indexOf("not ") === 0) {
      negate = true;
      q = q.slice(4).trim();
    }
    const typeMatch = /^(all|screen|print|speech)\b/.exec(q);
    if (typeMatch) {
      const type = typeMatch[1];
      q = q.slice(type.length).trim();
      if (q.indexOf("and") === 0) q = q.slice(3).trim();
      if (type === "print" || type === "speech") return negate;
      if (!q) return !negate;
    }
    const clauses = q.split(/\band\b/).map((c) => c.trim()).filter((c) => c.length > 0);
    const result = clauses.every((c) => matchClause(c));
    return negate ? !result : result;
  }
  function matches(query) {
    const list = String(query == null ? "" : query).split(",");
    return list.some((q) => matchSingle(q));
  }
  function installViewport(global) {
    const define = (name, get) => Object.defineProperty(global, name, { get, configurable: true });
    define("innerWidth", () => _width);
    define("innerHeight", () => _height);
    define("outerWidth", () => _width);
    define("outerHeight", () => _height);
    global.devicePixelRatio = 1;
    global.screen = {
      get width() {
        return _width;
      },
      get height() {
        return _height;
      },
      get availWidth() {
        return _width;
      },
      get availHeight() {
        return _height;
      },
      colorDepth: 24,
      pixelDepth: 24,
      orientation: {
        get type() {
          return _width >= _height ? "landscape-primary" : "portrait-primary";
        },
        angle: 0,
        addEventListener() {
        },
        removeEventListener() {
        }
      }
    };
    global.visualViewport = {
      get offsetLeft() {
        return 0;
      },
      get offsetTop() {
        return 0;
      },
      get pageLeft() {
        return 0;
      },
      get pageTop() {
        return 0;
      },
      get width() {
        return _width;
      },
      get height() {
        return _height;
      },
      get scale() {
        return 1;
      },
      onresize: null,
      onscroll: null,
      onscrollend: null,
      addEventListener() {
      },
      removeEventListener() {
      }
    };
    global.matchMedia = (query) => ({
      matches: matches(query),
      media: String(query == null ? "" : query),
      onchange: null,
      addListener() {
      },
      removeListener() {
      },
      addEventListener() {
      },
      removeEventListener() {
      },
      dispatchEvent() {
        return false;
      }
    });
  }

  // dom/Element.ts
  function attrNode(name, value, owner) {
    return { name, value, localName: name, namespaceURI: null, ownerElement: owner };
  }
  function nthAttrNode(attrs, index, owner) {
    let i = 0;
    for (const [name, value] of attrs) {
      if (i++ === index) return attrNode(name, value, owner);
    }
    return void 0;
  }
  var Element = class _Element extends Node {
    constructor(tag, ns) {
      super(1 /* Element */);
      this.attrs = /* @__PURE__ */ new Map();
      this.cachedInnerHTML = null;
      this._sheet = null;
      this.localName = String(tag).toLowerCase();
      this.tagName = this.localName.toUpperCase();
      this.nodeName = this.tagName;
      this.namespaceURI = ns || "http://www.w3.org/1999/xhtml";
      this.style = createStyleDeclaration();
      hideOwnFields(this);
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
    toggleAttribute(name, force) {
      const present = this.attrs.has(name);
      const add = force === void 0 ? !present : force;
      if (add) {
        if (!present) this.attrs.set(name, "");
        return true;
      }
      this.attrs.delete(name);
      return false;
    }
    getAttributeNames() {
      return Array.from(this.attrs.keys());
    }
    // A live NamedNodeMap-ish view: custom-element upgrade code walks `el.attributes` by index reading
    // `.length`/`[i].name`/`[i].value`, and React's singleton-attribute teardown does
    // `for (c = el.attributes; c.length;) el.removeAttributeNode(c[0])` — that loop only terminates if
    // `.length` is read live off the current attribute set, not snapshotted once when `.attributes` is accessed.
    get attributes() {
      const el = this;
      return new Proxy({}, {
        get(_t, prop) {
          if (prop === "length") return el.attrs.size;
          if (prop === "item") return (i) => nthAttrNode(el.attrs, i, el);
          if (prop === "getNamedItem") return (name) => el.getAttributeNode(name);
          if (typeof prop === "string" && /^\d+$/.test(prop)) return nthAttrNode(el.attrs, Number(prop), el);
          return void 0;
        }
      });
    }
    getAttributeNode(name) {
      return this.attrs.has(name) ? attrNode(name, this.attrs.get(name), this) : null;
    }
    setAttributeNode(attr) {
      if (attr && attr.name != null) this.attrs.set(String(attr.name), attr.value == null ? "" : String(attr.value));
      return null;
    }
    removeAttributeNode(attr) {
      if (attr && attr.name != null) this.attrs.delete(String(attr.name));
      return attr;
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
    matches(sel) {
      return matchesSelector(this, sel);
    }
    closest(sel) {
      let cur = this;
      while (cur) {
        if (cur.nodeType === 1 /* Element */ && matchesSelector(cur, sel)) return cur;
        cur = cur.parentNode;
      }
      return null;
    }
    getBoundingClientRect() {
      return { top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 };
    }
    // Element-level scroll no-ops, mirroring the window shims in browser/scroll.ts. The single-pass render
    // never scrolls, but a banner/nav component calls element.scrollTo({left,behavior}) during its init
    // (e.g. OneTrust/Astro islands), and a missing method throws and trips the surrounding error boundary.
    scrollTo() {
    }
    scrollBy() {
    }
    scroll() {
    }
    scrollIntoView() {
    }
    // jQuery gates .offset()/visibility on `getClientRects().length` before reading the box: a connected
    // element has one (zero-sized) rect, a detached one has none — matching the browser so the "is this laid
    // out?" branch takes the attached path instead of throwing on a missing method.
    getClientRects() {
      return this.isConnected ? [this.getBoundingClientRect()] : [];
    }
    // The viewport-sized box: jQuery's $(window).width() and many breakpoint helpers read the root element's
    // clientWidth/Height rather than window.innerWidth. Only the root (html/body) reports the viewport; every
    // other element is unlaid-out and reports 0, as in the always-zero getBoundingClientRect.
    get clientWidth() {
      return this.localName === "html" || this.localName === "body" ? viewportWidth() : 0;
    }
    get clientHeight() {
      return this.localName === "html" || this.localName === "body" ? viewportHeight() : 0;
    }
    // Unlike client* (0 for non-root), offset* must never be 0 or undefined here: layout-driven components
    // size themselves by dividing a container measurement by an element's offsetWidth (marquees duplicating
    // content to fill a row, virtualized lists computing an item count). A 0 or undefined denominator makes
    // that ratio NaN/Infinity, so the ensuing `new Array(count)` throws "Invalid array length" and trips the
    // SPA error boundary. A nonzero viewport-sized stand-in keeps the ratio finite (and small).
    get offsetWidth() {
      return viewportWidth();
    }
    get offsetHeight() {
      return viewportHeight();
    }
    get offsetTop() {
      return 0;
    }
    get offsetLeft() {
      return 0;
    }
    // null terminates the `while (el = el.offsetParent)` offset-accumulation idiom; a non-null value loops forever.
    get offsetParent() {
      return null;
    }
    // Web Animations: unlaid-out elements never animate, but the returned Animation is used synchronously
    // (cancel/play/pause, onfinish, currentTime), so a missing method would throw inside the effect that a
    // finite offsetWidth now lets run. Hand back an inert Animation instead.
    animate() {
      return {
        currentTime: 0,
        onfinish: null,
        oncancel: null,
        play() {
        },
        pause() {
        },
        cancel() {
        },
        finish() {
        },
        reverse() {
        },
        addEventListener() {
        },
        removeEventListener() {
        }
      };
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
        registerResource(el);
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
    get dir() {
      return this.attrs.get("dir") || "";
    }
    set dir(v) {
      this.attrs.set("dir", String(v));
    }
    get classList() {
      const read = () => (this.attrs.get("class") || "").split(/\s+/).filter(Boolean);
      const write = (tokens) => {
        this.attrs.set("class", tokens.join(" "));
      };
      return {
        add: (...names) => {
          const t = read();
          for (const n of names) if (t.indexOf(n) < 0) t.push(n);
          write(t);
        },
        remove: (...names) => {
          write(read().filter((x) => names.indexOf(x) < 0));
        },
        toggle: (name, force) => {
          const has = read().indexOf(name) >= 0;
          const next = force === void 0 ? !has : force;
          if (next && !has) write([...read(), name]);
          else if (!next && has) write(read().filter((x) => x !== name));
          return next;
        },
        replace: (oldName, newName) => {
          const t = read();
          const i = t.indexOf(oldName);
          if (i < 0) return false;
          t[i] = newName;
          write(t);
          return true;
        },
        contains: (name) => read().indexOf(name) >= 0,
        item: (i) => read()[i] ?? null,
        forEach: (cb) => read().forEach(cb),
        get length() {
          return read().length;
        },
        get value() {
          return read().join(" ");
        },
        toString: () => read().join(" ")
      };
    }
    get children() {
      return this.childNodes.filter((n) => n.nodeType === 1 /* Element */);
    }
    get childElementCount() {
      return this.children.length;
    }
    // Element-only traversal. Slider/drag libraries step through slides via nextElementSibling and cache
    // the track's parentElement/firstElementChild; a missing accessor returns undefined where they expect an
    // element-or-null, so the next `.removeAttribute`/`.classList` call throws instead of skipping.
    get parentElement() {
      const p = this.parentNode;
      return p && p.nodeType === 1 /* Element */ ? p : null;
    }
    get firstElementChild() {
      return this.children[0] || null;
    }
    get lastElementChild() {
      const kids = this.children;
      return kids[kids.length - 1] || null;
    }
    get nextElementSibling() {
      let n = this.nextSibling;
      while (n && n.nodeType !== 1 /* Element */) n = n.nextSibling;
      return n || null;
    }
    get previousElementSibling() {
      let n = this.previousSibling;
      while (n && n.nodeType !== 1 /* Element */) n = n.previousSibling;
      return n || null;
    }
    getElementsByClassName(className) {
      const out = [];
      collectByClass(this, String(className), out);
      return out;
    }
    get innerHTML() {
      return this.cachedInnerHTML != null ? this.cachedInnerHTML : serializeChildren(this);
    }
    // Parse into real child nodes (so cloneNode/lastChild/querySelector and the link collector see injected
    // content — CMS rich-text and dangerouslySetInnerHTML blocks carry anchors), then keep the verbatim
    // string as a serialization fast-path. Any later child mutation nulls the cache via appendChild et al.
    set innerHTML(v) {
      this.childNodes = [];
      const html = v == null ? "" : String(v);
      const parse = parserRef.parseFragment;
      if (parse) for (const node of parse(html)) this.appendChild(node);
      this.cachedInnerHTML = html;
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
    // Replaces this element with the parsed markup in its parent (custom elements self-unwrap via
    // `el.outerHTML = el.innerHTML`). A detached element has nowhere to go, so it's a no-op, matching the
    // getter-only shape bundles otherwise trip over.
    set outerHTML(v) {
      const parent = this.parentNode;
      if (!parent) return;
      const html = v == null ? "" : String(v);
      const parse = parserRef.parseFragment;
      const nodes = parse ? parse(html) : [];
      for (const node of nodes) parent.insertBefore(node, this);
      parent.removeChild(this);
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
    getAnimations() {
      return [];
    }
  };

  // dom/DocumentFragment.ts
  var DocumentFragment = class _DocumentFragment extends Node {
    constructor() {
      super(11 /* DocumentFragment */);
      hideOwnFields(this);
    }
    get nodeName() {
      return "#document-fragment";
    }
    querySelector(sel) {
      const r = querySelectorAll(this, sel);
      return r.length ? r[0] : null;
    }
    querySelectorAll(sel) {
      return querySelectorAll(this, sel);
    }
    getElementsByTagName(tag) {
      const out = [];
      collectByTag(this, String(tag).toLowerCase(), out);
      return out;
    }
    _shallowClone() {
      return new _DocumentFragment();
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

  // dom/HTMLElement.ts
  var ValidityStateAllValid = Object.freeze({
    valueMissing: false,
    typeMismatch: false,
    patternMismatch: false,
    tooLong: false,
    tooShort: false,
    rangeUnderflow: false,
    rangeOverflow: false,
    stepMismatch: false,
    badInput: false,
    customError: false,
    valid: true
  });
  var HTMLElement = class extends Element {
    constructor(tag, ns) {
      super(tag || customElements.currentName() || "", ns);
      this.shadowRoot = null;
      // Constraint Validation API. Frameworks grab a form control ref and call setCustomValidity during
      // render, so the methods must exist; they no-op and report valid.
      this.willValidate = true;
      this.validationMessage = "";
      hideOwnFields(this);
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
    focus() {
    }
    blur() {
    }
    get validity() {
      return ValidityStateAllValid;
    }
    checkValidity() {
      return true;
    }
    reportValidity() {
      return true;
    }
    setCustomValidity(_error) {
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

  // dom/Comment.ts
  var Comment = class _Comment extends CharacterData {
    constructor(data) {
      super(8 /* Comment */, data);
      hideOwnFields(this);
    }
    get nodeName() {
      return "#comment";
    }
    _shallowClone() {
      return new _Comment(this.data);
    }
  };

  // dom/DocumentType.ts
  var DocumentType = class _DocumentType extends Node {
    constructor(name, publicId = "", systemId = "") {
      super(10 /* DocumentType */);
      this.name = name;
      this.publicId = publicId;
      this.systemId = systemId;
      hideOwnFields(this);
    }
    get nodeName() {
      return this.name;
    }
    _shallowClone() {
      return new _DocumentType(this.name, this.publicId, this.systemId);
    }
  };

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

  // url/URL.ts
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

  // dom/HTMLAnchorElement.ts
  var HTMLAnchorElement = class extends HTMLElement {
    constructor() {
      super("a");
    }
    get href() {
      const raw = this.getAttribute("href");
      if (raw == null) return "";
      try {
        return new URL(raw).href;
      } catch {
        return raw;
      }
    }
    set href(value) {
      this.setAttribute("href", value == null ? "" : String(value));
    }
    resolved() {
      const raw = this.getAttribute("href");
      if (!raw) return null;
      try {
        return new URL(raw);
      } catch {
        return null;
      }
    }
    get protocol() {
      return this.resolved()?.protocol ?? "";
    }
    get host() {
      return this.resolved()?.host ?? "";
    }
    get hostname() {
      return this.resolved()?.hostname ?? "";
    }
    get port() {
      return this.resolved()?.port ?? "";
    }
    get pathname() {
      return this.resolved()?.pathname ?? "";
    }
    get search() {
      return this.resolved()?.search ?? "";
    }
    get hash() {
      return this.resolved()?.hash ?? "";
    }
    get origin() {
      return this.resolved()?.origin ?? "";
    }
  };

  // dom/HTMLScriptElement.ts
  var HTMLScriptElement = class extends HTMLElement {
    constructor() {
      super("script");
    }
    get src() {
      const raw = this.getAttribute("src");
      if (raw == null) return "";
      try {
        return new URL(raw).href;
      } catch {
        return raw;
      }
    }
    set src(value) {
      this.setAttribute("src", value == null ? "" : String(value));
    }
    get type() {
      return this.getAttribute("type") || "";
    }
    set type(value) {
      this.setAttribute("type", value == null ? "" : String(value));
    }
  };

  // dom/HTMLLinkElement.ts
  var HTMLLinkElement = class extends HTMLElement {
    constructor() {
      super("link");
    }
    get href() {
      return this.getAttribute("href") || "";
    }
    set href(value) {
      this.setAttribute("href", value == null ? "" : String(value));
    }
    get rel() {
      return this.getAttribute("rel") || "";
    }
    set rel(value) {
      this.setAttribute("rel", value == null ? "" : String(value));
    }
  };

  // dom/HTMLSelectElement.ts
  var HTMLSelectElement = class extends HTMLElement {
    constructor() {
      super("select");
    }
    get options() {
      return this.getElementsByTagName("option");
    }
  };

  // dom/HTMLOptionElement.ts
  var HTMLOptionElement = class extends HTMLElement {
    constructor() {
      super("option");
    }
    get value() {
      const v = this.getAttribute("value");
      return v != null ? v : this.textContent;
    }
    set value(v) {
      this.setAttribute("value", v == null ? "" : String(v));
    }
  };

  // dom/HTMLImageElement.ts
  var HTMLImageElement = class extends HTMLElement {
    constructor() {
      super("img");
    }
    get alt() {
      return this.getAttribute("alt") || "";
    }
    set alt(value) {
      this.setAttribute("alt", value == null ? "" : String(value));
    }
    get src() {
      return this.getAttribute("src") || "";
    }
    set src(value) {
      this.setAttribute("src", value == null ? "" : String(value));
    }
  };

  // dom/HTMLIFrameElement.ts
  var HTMLIFrameElement = class extends HTMLElement {
    constructor() {
      super("iframe");
    }
    get src() {
      return this.getAttribute("src") || "";
    }
    set src(value) {
      this.setAttribute("src", value == null ? "" : String(value));
    }
    get contentWindow() {
      let win = this._contentWindow;
      if (!win) {
        win = {
          postMessage() {
          },
          close() {
          },
          focus() {
          },
          blur() {
          }
        };
        Object.defineProperty(this, "_contentWindow", { value: win, enumerable: false });
      }
      return win;
    }
    get contentDocument() {
      return null;
    }
  };

  // dom/HTMLMediaElement.ts
  var HTMLMediaElement = class extends HTMLElement {
    get currentTime() {
      return 0;
    }
    set currentTime(_value) {
    }
    get paused() {
      return true;
    }
    get src() {
      return this.getAttribute("src") || "";
    }
    set src(value) {
      this.setAttribute("src", value == null ? "" : String(value));
    }
    get muted() {
      return this.hasAttribute("muted");
    }
    set muted(value) {
      if (value) this.setAttribute("muted", "");
      else this.removeAttribute("muted");
    }
    load() {
    }
    play() {
      return Promise.resolve();
    }
    pause() {
    }
    canPlayType() {
      return "";
    }
  };

  // dom/HTMLVideoElement.ts
  var HTMLVideoElement = class extends HTMLMediaElement {
    constructor() {
      super("video");
    }
    get poster() {
      return this.getAttribute("poster") || "";
    }
    set poster(value) {
      this.setAttribute("poster", value == null ? "" : String(value));
    }
  };

  // dom/HTMLAudioElement.ts
  var HTMLAudioElement = class extends HTMLMediaElement {
    constructor() {
      super("audio");
    }
  };

  // dom/HTMLDialogElement.ts
  var HTMLDialogElement = class extends HTMLElement {
    constructor() {
      super("dialog");
      this.returnValue = "";
    }
    get open() {
      return this.hasAttribute("open");
    }
    set open(value) {
      if (value) this.setAttribute("open", "");
      else this.removeAttribute("open");
    }
    show() {
      this.setAttribute("open", "");
    }
    showModal() {
      this.setAttribute("open", "");
    }
    close(returnValue) {
      this.removeAttribute("open");
      if (returnValue !== void 0) this.returnValue = String(returnValue);
    }
  };

  // dom/webgl.ts
  var _enabled = false;
  function enableWebGl() {
    _enabled = true;
  }
  function isWebGlEnabled() {
    return _enabled;
  }
  function isWebGlContextType(type) {
    return type === "webgl" || type === "webgl2" || type === "experimental-webgl" || type === "experimental-webgl2";
  }
  var CONSTANTS = {
    VENDOR: 7936,
    RENDERER: 7937,
    VERSION: 7938,
    SHADING_LANGUAGE_VERSION: 35724,
    UNMASKED_VENDOR_WEBGL: 37445,
    UNMASKED_RENDERER_WEBGL: 37446,
    MAX_TEXTURE_SIZE: 3379,
    MAX_CUBE_MAP_TEXTURE_SIZE: 34076,
    MAX_RENDERBUFFER_SIZE: 34024,
    MAX_3D_TEXTURE_SIZE: 32883,
    MAX_ARRAY_TEXTURE_LAYERS: 35071,
    MAX_VIEWPORT_DIMS: 3386,
    MAX_VERTEX_ATTRIBS: 34921,
    MAX_VERTEX_UNIFORM_VECTORS: 36347,
    MAX_VARYING_VECTORS: 36348,
    MAX_FRAGMENT_UNIFORM_VECTORS: 36349,
    MAX_TEXTURE_IMAGE_UNITS: 34930,
    MAX_VERTEX_TEXTURE_IMAGE_UNITS: 35660,
    MAX_COMBINED_TEXTURE_IMAGE_UNITS: 35661,
    MAX_TEXTURE_MAX_ANISOTROPY_EXT: 34047,
    MAX_DRAW_BUFFERS: 34852,
    MAX_COLOR_ATTACHMENTS: 36063,
    MAX_SAMPLES: 36183,
    ALIASED_LINE_WIDTH_RANGE: 33902,
    ALIASED_POINT_SIZE_RANGE: 33901,
    SAMPLES: 32937,
    SAMPLE_BUFFERS: 32936,
    RED_BITS: 3410,
    GREEN_BITS: 3411,
    BLUE_BITS: 3412,
    ALPHA_BITS: 3413,
    DEPTH_BITS: 3414,
    STENCIL_BITS: 3415,
    SUBPIXEL_BITS: 3408,
    COMPILE_STATUS: 35713,
    LINK_STATUS: 35714,
    VALIDATE_STATUS: 35715,
    DELETE_STATUS: 35712,
    ACTIVE_UNIFORMS: 35718,
    ACTIVE_ATTRIBUTES: 35721,
    FRAMEBUFFER_COMPLETE: 36053,
    NO_ERROR: 0
  };
  var _nextSynthetic = 2415919104;
  function constantFor(name) {
    let value = CONSTANTS[name];
    if (value === void 0) {
      value = _nextSynthetic++;
      CONSTANTS[name] = value;
    }
    return value;
  }
  function getParameterValue(pname, isWebGl2) {
    switch (pname) {
      case CONSTANTS.VERSION:
        return isWebGl2 ? "WebGL 2.0" : "WebGL 1.0";
      case CONSTANTS.SHADING_LANGUAGE_VERSION:
        return isWebGl2 ? "WebGL GLSL ES 3.00" : "WebGL GLSL ES 1.0";
      case CONSTANTS.VENDOR:
      case CONSTANTS.UNMASKED_VENDOR_WEBGL:
        return "SimpleCrawler";
      case CONSTANTS.RENDERER:
      case CONSTANTS.UNMASKED_RENDERER_WEBGL:
        return "SimpleCrawler WebGL";
      case CONSTANTS.MAX_TEXTURE_SIZE:
      case CONSTANTS.MAX_CUBE_MAP_TEXTURE_SIZE:
      case CONSTANTS.MAX_RENDERBUFFER_SIZE:
      case CONSTANTS.MAX_3D_TEXTURE_SIZE:
        return 4096;
      case CONSTANTS.MAX_VIEWPORT_DIMS:
        return new Int32Array([4096, 4096]);
      case CONSTANTS.MAX_VERTEX_ATTRIBS:
      case CONSTANTS.MAX_TEXTURE_IMAGE_UNITS:
      case CONSTANTS.MAX_VERTEX_TEXTURE_IMAGE_UNITS:
      case CONSTANTS.MAX_TEXTURE_MAX_ANISOTROPY_EXT:
        return 16;
      case CONSTANTS.MAX_COMBINED_TEXTURE_IMAGE_UNITS:
        return 32;
      case CONSTANTS.MAX_VERTEX_UNIFORM_VECTORS:
      case CONSTANTS.MAX_FRAGMENT_UNIFORM_VECTORS:
        return 1024;
      case CONSTANTS.MAX_VARYING_VECTORS:
        return 30;
      case CONSTANTS.MAX_DRAW_BUFFERS:
      case CONSTANTS.MAX_COLOR_ATTACHMENTS:
        return 8;
      case CONSTANTS.MAX_ARRAY_TEXTURE_LAYERS:
        return 256;
      case CONSTANTS.MAX_SAMPLES:
        return 4;
      case CONSTANTS.ALIASED_LINE_WIDTH_RANGE:
      case CONSTANTS.ALIASED_POINT_SIZE_RANGE:
        return new Float32Array([1, 1024]);
      case CONSTANTS.RED_BITS:
      case CONSTANTS.GREEN_BITS:
      case CONSTANTS.BLUE_BITS:
      case CONSTANTS.ALPHA_BITS:
        return 8;
      case CONSTANTS.DEPTH_BITS:
        return 24;
      case CONSTANTS.STENCIL_BITS:
        return 8;
      case CONSTANTS.SUBPIXEL_BITS:
        return 4;
      default:
        return 0;
    }
  }
  var _noop = () => {
  };
  function handle() {
    return {};
  }
  function stub(backing) {
    return new Proxy(backing, {
      get(target, prop, receiver) {
        if (prop in target) return Reflect.get(target, prop, receiver);
        if (typeof prop === "symbol") return void 0;
        const name = String(prop);
        if (/^[0-9A-Z_]+$/.test(name)) return constantFor(name);
        return _noop;
      }
    });
  }
  var _extensions = {};
  function getExtension(name) {
    return _extensions[name] || (_extensions[name] = stub({ name }));
  }
  function createWebGLContext(canvas, contextType, attributes) {
    const isWebGl2 = contextType === "webgl2" || contextType === "experimental-webgl2";
    const contextAttributes = {
      alpha: true,
      antialias: true,
      depth: true,
      premultipliedAlpha: true,
      preserveDrawingBuffer: false,
      stencil: false,
      ...attributes && typeof attributes === "object" ? attributes : {}
    };
    const impl = {
      canvas,
      drawingBufferWidth: canvas && canvas.width ? canvas.width : 300,
      drawingBufferHeight: canvas && canvas.height ? canvas.height : 150,
      getContextAttributes: () => contextAttributes,
      isContextLost: () => false,
      getError: () => 0,
      getParameter: (pname) => getParameterValue(pname, isWebGl2),
      getExtension: (name) => getExtension(name),
      getSupportedExtensions: () => [],
      getShaderPrecisionFormat: () => ({ rangeMin: 127, rangeMax: 127, precision: 23 }),
      createShader: handle,
      createProgram: handle,
      createBuffer: handle,
      createTexture: handle,
      createFramebuffer: handle,
      createRenderbuffer: handle,
      createVertexArray: handle,
      createSampler: handle,
      createQuery: handle,
      createTransformFeedback: handle,
      fenceSync: handle,
      // Compilation, linking and framebuffer completeness must report success or the library aborts setup.
      getShaderParameter: (_shader, pname) => pname === CONSTANTS.COMPILE_STATUS ? true : 0,
      getProgramParameter: (_program, pname) => pname === CONSTANTS.LINK_STATUS || pname === CONSTANTS.VALIDATE_STATUS ? true : 0,
      checkFramebufferStatus: () => CONSTANTS.FRAMEBUFFER_COMPLETE,
      getShaderInfoLog: () => "",
      getProgramInfoLog: () => "",
      // A non-null uniform location keeps the library on its "uniform exists, set it" path; attrib slots are
      // plain indices. Both are only stored and re-passed, so any stable value works.
      getUniformLocation: () => ({}),
      getAttribLocation: () => 0,
      getActiveUniform: () => null,
      getActiveAttrib: () => null
    };
    for (const name in CONSTANTS) impl[name] = CONSTANTS[name];
    return stub(impl);
  }

  // dom/HTMLCanvasElement.ts
  function createContext2D(canvas) {
    const noop = () => {
    };
    return {
      canvas,
      fillStyle: "#000000",
      strokeStyle: "#000000",
      globalAlpha: 1,
      globalCompositeOperation: "source-over",
      lineWidth: 1,
      lineCap: "butt",
      lineJoin: "miter",
      font: "10px sans-serif",
      textAlign: "start",
      textBaseline: "alphabetic",
      save: noop,
      restore: noop,
      scale: noop,
      rotate: noop,
      translate: noop,
      transform: noop,
      setTransform: noop,
      resetTransform: noop,
      beginPath: noop,
      closePath: noop,
      moveTo: noop,
      lineTo: noop,
      bezierCurveTo: noop,
      quadraticCurveTo: noop,
      arc: noop,
      arcTo: noop,
      ellipse: noop,
      rect: noop,
      fill: noop,
      stroke: noop,
      clip: noop,
      fillRect: noop,
      strokeRect: noop,
      clearRect: noop,
      fillText: noop,
      strokeText: noop,
      drawImage: noop,
      putImageData: noop,
      setLineDash: noop,
      getLineDash: () => [],
      measureText: () => ({ width: 0, actualBoundingBoxAscent: 0, actualBoundingBoxDescent: 0 }),
      createLinearGradient: () => ({ addColorStop: noop }),
      createRadialGradient: () => ({ addColorStop: noop }),
      createPattern: () => null,
      createImageData: (w, h) => ({ width: w || 0, height: h || 0, data: new Uint8ClampedArray(Math.max(0, (w || 0) * (h || 0) * 4)) }),
      getImageData: (_x, _y, w, h) => ({ width: w || 0, height: h || 0, data: new Uint8ClampedArray(Math.max(0, (w || 0) * (h || 0) * 4)) })
    };
  }
  var HTMLCanvasElement = class extends HTMLElement {
    constructor() {
      super("canvas");
    }
    get width() {
      const v = parseInt(this.getAttribute("width") || "", 10);
      return isNaN(v) ? 300 : v;
    }
    set width(value) {
      this.setAttribute("width", String(value == null ? 0 : value));
    }
    get height() {
      const v = parseInt(this.getAttribute("height") || "", 10);
      return isNaN(v) ? 150 : v;
    }
    set height(value) {
      this.setAttribute("height", String(value == null ? 0 : value));
    }
    getContext(type, attributes) {
      if (type === "2d") return createContext2D(this);
      if (isWebGlContextType(type) && isWebGlEnabled()) return createWebGLContext(this, type, attributes);
      return null;
    }
    toDataURL() {
      return "data:,";
    }
    toBlob(callback) {
      if (typeof callback === "function") callback(null);
    }
  };

  // dom/reflectedElements.ts
  var reflectedElementFactories = {
    a: () => new HTMLAnchorElement(),
    script: () => new HTMLScriptElement(),
    link: () => new HTMLLinkElement(),
    select: () => new HTMLSelectElement(),
    option: () => new HTMLOptionElement(),
    img: () => new HTMLImageElement(),
    iframe: () => new HTMLIFrameElement(),
    video: () => new HTMLVideoElement(),
    audio: () => new HTMLAudioElement(),
    dialog: () => new HTMLDialogElement(),
    canvas: () => new HTMLCanvasElement()
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
  function isHtmlSpace(c) {
    return c === 32 || c === 9 || c === 10 || c === 13 || c === 12;
  }
  function skipSpace(src, i, len) {
    while (i < len && isHtmlSpace(src.charCodeAt(i))) i++;
    return i;
  }
  function isAlpha(c) {
    return c >= 65 && c <= 90 || c >= 97 && c <= 122;
  }
  function matchTagName(src, start, len) {
    if (start >= len || !isAlpha(src.charCodeAt(start))) return -1;
    let i = start + 1;
    while (i < len) {
      const c = src.charCodeAt(i);
      if (c >= 65 && c <= 90 || c >= 97 && c <= 122 || c >= 48 && c <= 57 || c === 45 || c === 58 || c === 95) i++;
      else break;
    }
    return i;
  }
  function scanAttrName(src, i, len) {
    while (i < len) {
      const c = src.charCodeAt(i);
      if (isHtmlSpace(c) || c === 47 || c === 62 || c === 34 || c === 39 || c === 60 || c === 61) break;
      i++;
    }
    return i;
  }
  function scanBareValue(src, i, len) {
    while (i < len) {
      const c = src.charCodeAt(i);
      if (isHtmlSpace(c) || c === 62) break;
      i++;
    }
    return i;
  }
  function findRawTextClose(input, tag, from) {
    return indexOfCI(input, "</" + tag, from);
  }

  // html/parser.ts
  function createLocalElement(tag) {
    const factory = reflectedElementFactories[tag];
    return factory ? factory() : new HTMLElement(tag);
  }
  function attachChild(parent, child) {
    child.parentNode = parent;
    parent.childNodes.push(child);
  }
  function wireDocument(doc2, root, head, body) {
    doc2.documentElement = root;
    doc2.head = head;
    doc2.body = body;
    root.parentNode = doc2;
    doc2.childNodes = [root];
  }
  function parseHTML(doc2, input) {
    const src = input == null ? "" : String(input);
    const len = src.length;
    const root = new HTMLElement("html");
    const head = new HTMLElement("head");
    const body = new HTMLElement("body");
    attachChild(root, head);
    attachChild(root, body);
    let open = [body];
    function appendText(parent, text) {
      const last = parent.childNodes[parent.childNodes.length - 1];
      if (last && last.nodeType === 3 /* Text */) last.data += text;
      else attachChild(parent, new Text(text));
    }
    let i = 0;
    while (i < len) {
      if (src.charCodeAt(i) !== 60) {
        let textEnd = src.indexOf("<", i);
        if (textEnd < 0) textEnd = len;
        appendText(open[open.length - 1], decodeEntities(src.slice(i, textEnd)));
        i = textEnd;
        continue;
      }
      const c1 = i + 1 < len ? src.charCodeAt(i + 1) : -1;
      if (isAlpha(c1)) {
        const nameEnd = matchTagName(src, i + 1, len);
        const tag = src.slice(i + 1, nameEnd).toLowerCase();
        let j = nameEnd;
        const structural = tag === "html" || tag === "head" || tag === "body";
        let el;
        if (tag === "html") el = root;
        else if (tag === "head") {
          open = [head];
          el = head;
        } else if (tag === "body") {
          open = [body];
          el = body;
        } else el = createLocalElement(tag);
        let selfClosed = false;
        while (j < len) {
          j = skipSpace(src, j, len);
          if (j >= len) break;
          const c = src.charCodeAt(j);
          if (c === 62) {
            j++;
            break;
          }
          if (c === 47 && src.charCodeAt(j + 1) === 62) {
            selfClosed = true;
            j += 2;
            break;
          }
          const nameE = scanAttrName(src, j, len);
          if (nameE === j) {
            j++;
            continue;
          }
          const an = src.slice(j, nameE).toLowerCase();
          j = skipSpace(src, nameE, len);
          let val = "";
          if (src.charCodeAt(j) === 61) {
            j = skipSpace(src, j + 1, len);
            const q = src.charCodeAt(j);
            if (q === 34 || q === 39) {
              const qEnd = src.indexOf(src[j], j + 1);
              val = decodeEntities(qEnd < 0 ? src.slice(j + 1) : src.slice(j + 1, qEnd));
              j = qEnd < 0 ? len : qEnd + 1;
            } else {
              const vEnd = scanBareValue(src, j, len);
              val = decodeEntities(src.slice(j, vEnd));
              j = vEnd;
            }
          }
          el.setAttribute(an, val);
        }
        i = j;
        if (structural) continue;
        if (RAWTEXT_ELEMENTS[tag]) {
          const rawFrom = j;
          const rawTo = findRawTextClose(src, tag, rawFrom);
          const raw = rawTo < 0 ? src.slice(rawFrom) : src.slice(rawFrom, rawTo);
          if (raw) attachChild(el, new Text(raw));
          const rawGt = rawTo < 0 ? len : src.indexOf(">", rawTo);
          i = rawGt < 0 ? len : rawGt + 1;
          attachChild(open[open.length - 1], el);
          continue;
        }
        attachChild(open[open.length - 1], el);
        if (!VOID_ELEMENTS[tag] && !selfClosed) open.push(el);
        continue;
      }
      if (c1 === 47) {
        const nameEnd = matchTagName(src, i + 2, len);
        if (nameEnd >= 0) {
          const closeName = src.slice(i + 2, nameEnd).toLowerCase();
          for (let k = open.length - 1; k > 0; k--) {
            if (open[k].localName === closeName) {
              open.length = k;
              break;
            }
          }
        }
        const gt = src.indexOf(">", i);
        i = gt < 0 ? len : gt + 1;
        continue;
      }
      if (c1 === 33 && src.startsWith("<!--", i)) {
        const cEnd = src.indexOf("-->", i + 4);
        attachChild(open[open.length - 1], new Comment(src.slice(i + 4, cEnd < 0 ? len : cEnd)));
        i = cEnd < 0 ? len : cEnd + 3;
        continue;
      }
      if (c1 === 33 || c1 === 63) {
        const declEnd = src.indexOf(">", i);
        const end = declEnd < 0 ? len : declEnd;
        const inner = src.slice(i + 2, end);
        if (c1 === 63) attachChild(open[open.length - 1], new Comment("?" + inner));
        else if (!/^doctype/i.test(inner)) attachChild(open[open.length - 1], new Comment(inner));
        i = declEnd < 0 ? len : declEnd + 1;
        continue;
      }
      appendText(open[open.length - 1], "<");
      i++;
    }
    wireDocument(doc2, root, head, body);
    return root;
  }
  function parseFragment(html) {
    const scratch = {};
    parseHTML(scratch, html);
    const kids = scratch.body.childNodes.slice();
    for (const k of kids) k.parentNode = null;
    return kids;
  }
  parserRef.parseFragment = parseFragment;

  // dom/Range.ts
  var _zeroRect = { top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 };
  var Range = class _Range {
    constructor() {
      this.startContainer = null;
      this.endContainer = null;
      this.startOffset = 0;
      this.endOffset = 0;
      this.collapsed = true;
      this.commonAncestorContainer = null;
    }
    setStart(node, offset) {
      this.startContainer = node;
      this.startOffset = offset;
    }
    setEnd(node, offset) {
      this.endContainer = node;
      this.endOffset = offset;
    }
    setStartBefore(node) {
      this.startContainer = node;
    }
    setStartAfter(node) {
      this.startContainer = node;
    }
    setEndBefore(node) {
      this.endContainer = node;
    }
    setEndAfter(node) {
      this.endContainer = node;
    }
    selectNode(node) {
      this.startContainer = this.endContainer = this.commonAncestorContainer = node;
    }
    selectNodeContents(node) {
      this.startContainer = this.endContainer = this.commonAncestorContainer = node;
    }
    collapse() {
    }
    cloneRange() {
      return new _Range();
    }
    detach() {
    }
    insertNode(node) {
      if (this.startContainer && typeof this.startContainer.appendChild === "function") this.startContainer.appendChild(node);
    }
    deleteContents() {
    }
    cloneContents() {
      return new DocumentFragment();
    }
    extractContents() {
      return new DocumentFragment();
    }
    surroundContents() {
    }
    getBoundingClientRect() {
      return _zeroRect;
    }
    getClientRects() {
      return [];
    }
    createContextualFragment(html) {
      const fragment = new DocumentFragment();
      for (const node of parseFragment(html)) fragment.appendChild(node);
      return fragment;
    }
  };

  // dom/HTMLTemplateElement.ts
  var HTMLTemplateElement = class _HTMLTemplateElement extends Element {
    constructor() {
      super("template");
      this.content = new DocumentFragment();
      hideOwnFields(this);
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

  // dom/Document.ts
  var Document = class _Document extends Node {
    constructor(defaultView) {
      super(9 /* Document */);
      this.documentElement = null;
      this.head = null;
      this.body = null;
      this.styleSheets = [];
      // The <script> currently executing, set by the host around each classic script. Next's webpack
      // auto-public-path asserts it `instanceof HTMLScriptElement` and reads its src; outside execution it's null.
      this.currentScript = null;
      // Real navigation transitions loading→interactive→complete over time; this render parses the whole
      // document synchronously before any script runs, so by the time script code can observe it there is
      // nothing left "loading" — frameworks that gate on readyState (Next's Flight stream close among them)
      // see "complete" immediately instead of stalling behind a state that never advances.
      this.readyState = "complete";
      this._cookies = /* @__PURE__ */ new Map();
      this.defaultView = defaultView || null;
      hideOwnFields(this);
    }
    // Browsers expose document.location as an alias of window.location; scripts (analytics, Clerk's CDN
    // loader) read document.location.protocol/href, which threw on undefined when only window.location existed.
    get location() {
      return this.defaultView ? this.defaultView.location : null;
    }
    // Bundles read document.referrer as a string (analytics, `referrer.split('/')[2] !== location.host`);
    // a single-pass render has no navigation history, so it's always the empty string.
    get referrer() {
      return "";
    }
    // document.domain mirrors the origin's hostname; scripts split/compare it and throw their own error when
    // it's undefined. The setter (legacy same-origin relaxation) is accepted and ignored — a single-pass render
    // never makes cross-origin calls that would consult it.
    get domain() {
      const loc = this.location;
      return loc && loc.hostname ? String(loc.hostname) : "";
    }
    set domain(_value) {
    }
    // A real document.cookie is always a string. Bundles probe it (document.cookie.includes(...)) and set it;
    // we keep a name→value store, ignoring attributes (path/expires/domain) and expiry since rendering is a
    // single synchronous pass.
    get cookie() {
      const out = [];
      for (const [k, v] of this._cookies) out.push(`${k}=${v}`);
      return out.join("; ");
    }
    set cookie(value) {
      const pair = String(value ?? "").split(";")[0];
      const eq = pair.indexOf("=");
      if (eq < 0) return;
      const name = pair.slice(0, eq).trim();
      if (name) this._cookies.set(name, pair.slice(eq + 1).trim());
    }
    createElement(tag) {
      const name = String(tag).toLowerCase();
      if (name === "template") return new HTMLTemplateElement();
      const factory = reflectedElementFactories[name];
      if (factory) return factory();
      const custom = customElements.tryCreate(name);
      return custom || new HTMLElement(name);
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
    createRange() {
      return new Range();
    }
    getElementById(id) {
      return walkFind(this.documentElement, (e) => e.getAttribute("id") === id);
    }
    getElementsByTagName(tag) {
      const out = [];
      if (this.documentElement) collectByTag(this.documentElement, String(tag).toLowerCase(), out);
      return out;
    }
    getElementsByClassName(className) {
      const out = [];
      if (this.documentElement) collectByClass(this.documentElement, String(className), out);
      return out;
    }
    getElementsByName(name) {
      const out = [];
      if (this.documentElement) collectByPredicate(this.documentElement, (e) => e.getAttribute("name") === name, out);
      return out;
    }
    get scripts() {
      return this.getElementsByTagName("script");
    }
    querySelector(sel) {
      const r = querySelectorAll(this, sel);
      return r.length ? r[0] : null;
    }
    querySelectorAll(sel) {
      return querySelectorAll(this, sel);
    }
    createEvent() {
      return { initEvent() {
      } };
    }
    // jQuery's UMD factory feature-detects against `implementation.createHTMLDocument` during init; a missing
    // implementation threw before the global was assigned, so later bundles saw "jQuery is not defined".
    get implementation() {
      return {
        hasFeature: () => true,
        createDocumentType: (name, publicId, systemId) => new DocumentType(name, publicId ?? "", systemId ?? ""),
        createHTMLDocument: (title) => {
          const d = new _Document();
          const html = d.createElement("html");
          const head = d.createElement("head");
          const body = d.createElement("body");
          html.appendChild(head);
          html.appendChild(body);
          d.appendChild(html);
          d.documentElement = html;
          d.head = head;
          d.body = body;
          if (title) {
            const t = d.createElement("title");
            t.textContent = title;
            head.appendChild(t);
          }
          return d;
        }
      };
    }
    get nodeName() {
      return "#document";
    }
    get ownerDocument() {
      return null;
    }
    _shallowClone() {
      return new _Document(this.defaultView);
    }
  };

  // dom/Animation.ts
  var Animation = class extends EventTarget {
  };

  // dom/CSSTransition.ts
  var CSSTransition = class extends Animation {
    get transitionProperty() {
      return "";
    }
  };

  // dom/htmlInterfaces.ts
  var htmlInterfaces_exports = {};
  __export(htmlInterfaces_exports, {
    HTMLAnchorElement: () => HTMLAnchorElement,
    HTMLAudioElement: () => HTMLAudioElement,
    HTMLButtonElement: () => HTMLButtonElement,
    HTMLCanvasElement: () => HTMLCanvasElement,
    HTMLDialogElement: () => HTMLDialogElement,
    HTMLFormElement: () => HTMLFormElement,
    HTMLIFrameElement: () => HTMLIFrameElement,
    HTMLImageElement: () => HTMLImageElement,
    HTMLInputElement: () => HTMLInputElement,
    HTMLLinkElement: () => HTMLLinkElement,
    HTMLMediaElement: () => HTMLMediaElement,
    HTMLOptionElement: () => HTMLOptionElement,
    HTMLScriptElement: () => HTMLScriptElement,
    HTMLSelectElement: () => HTMLSelectElement,
    HTMLStyleElement: () => HTMLStyleElement,
    HTMLTextAreaElement: () => HTMLTextAreaElement,
    HTMLUnknownElement: () => HTMLUnknownElement,
    HTMLVideoElement: () => HTMLVideoElement,
    MathMLElement: () => MathMLElement,
    SVGElement: () => SVGElement,
    SVGSVGElement: () => SVGSVGElement
  });
  var HTMLInputElement = class extends HTMLElement {
  };
  var HTMLTextAreaElement = class extends HTMLElement {
  };
  var HTMLButtonElement = class extends HTMLElement {
  };
  var HTMLFormElement = class extends HTMLElement {
  };
  var HTMLStyleElement = class extends HTMLElement {
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
    language: "en",
    geolocation: {
      getCurrentPosition() {
      },
      watchPosition() {
        return 0;
      },
      clearWatch() {
      }
    }
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
  var _longTimerMs = 4e3;
  var _seq = 0;
  var _tasks = [];
  var _byId2 = /* @__PURE__ */ new Map();
  function enqueue(cb) {
    if (typeof cb !== "function") return 0;
    const id = ++_seq;
    const task = { id, cb, cancelled: false };
    _tasks.push(task);
    _byId2.set(id, task);
    return id;
  }
  function cancel(id) {
    if (typeof id !== "number") return;
    const task = _byId2.get(id);
    if (task) {
      task.cancelled = true;
      _byId2.delete(id);
    }
  }
  function pendingCount() {
    return _tasks.length;
  }
  function pumpTasks() {
    if (!_tasks.length) return 0;
    const batch = _tasks.splice(0, _tasks.length);
    for (const task of batch) {
      _byId2.delete(task.id);
      if (task.cancelled) continue;
      try {
        task.cb();
      } catch (e) {
        reportSwallowed("scheduled task", e);
      }
    }
    return _tasks.length;
  }
  function installTimerGlobals(global) {
    global.queueMicrotask = (cb) => enqueue(cb);
    global.setTimeout = (cb, delay) => typeof delay === "number" && delay > _longTimerMs ? ++_seq : enqueue(cb);
    global.clearTimeout = (id) => cancel(id);
    global.setInterval = () => 0;
    global.clearInterval = () => {
    };
    global.requestAnimationFrame = (cb) => enqueue(() => cb(0));
    global.cancelAnimationFrame = (id) => cancel(id);
  }

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
  var _hostPerf = globalThis.Performance;
  var _hostNow = _hostPerf && typeof _hostPerf.now === "function" ? () => _hostPerf.now() : null;
  var startTime = _hostNow ? _hostNow() : Date.now();
  var Performance = class {
    constructor() {
      this.timeOrigin = startTime;
    }
    now() {
      return _hostNow ? _hostNow() - startTime : Date.now() - startTime;
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

  // browser/IntersectionObserverEntry.ts
  var _zeroRect2 = Object.freeze({ top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 });
  var IntersectionObserverEntry = class {
    get boundingClientRect() {
      return _zeroRect2;
    }
    get intersectionRect() {
      return _zeroRect2;
    }
    get rootBounds() {
      return null;
    }
    get intersectionRatio() {
      return 0;
    }
    get isIntersecting() {
      return false;
    }
    get target() {
      return null;
    }
    get time() {
      return 0;
    }
  };

  // browser/IntersectionObserver.ts
  var IntersectionObserver = class {
    constructor(callback) {
      this._pending = [];
      this._scheduled = false;
      this._callback = typeof callback === "function" ? callback : () => {
      };
    }
    observe(target) {
      const rect = target && typeof target.getBoundingClientRect === "function" ? target.getBoundingClientRect() : _zeroRect2;
      this._pending.push({
        target,
        isIntersecting: true,
        intersectionRatio: 1,
        boundingClientRect: rect,
        intersectionRect: rect,
        rootBounds: rect,
        time: 0
      });
      if (!this._scheduled) {
        this._scheduled = true;
        enqueue(() => this._flush());
      }
    }
    unobserve() {
    }
    disconnect() {
      this._pending = [];
      this._scheduled = false;
    }
    takeRecords() {
      return [];
    }
    _flush() {
      this._scheduled = false;
      if (!this._pending.length) return;
      const entries = this._pending;
      this._pending = [];
      this._callback(entries, this);
    }
  };

  // browser/Blob.ts
  var _encoder = new TextEncoder();
  var _decoder = new TextDecoder();
  function partBytes(part) {
    if (part instanceof Blob) return part._bytes();
    if (part instanceof Uint8Array) return part;
    if (part instanceof ArrayBuffer) return new Uint8Array(part);
    if (part && ArrayBuffer.isView(part)) return new Uint8Array(part.buffer, part.byteOffset, part.byteLength);
    return _encoder.encode(part == null ? "" : String(part));
  }
  var Blob = class _Blob {
    constructor(parts, options) {
      this._parts = (parts || []).map(partBytes);
      this.type = options && options.type != null ? String(options.type).toLowerCase() : "";
    }
    get size() {
      let n = 0;
      for (const p of this._parts) n += p.length;
      return n;
    }
    _bytes() {
      const out = new Uint8Array(this.size);
      let at = 0;
      for (const p of this._parts) {
        out.set(p, at);
        at += p.length;
      }
      return out;
    }
    arrayBuffer() {
      return Promise.resolve(this._bytes().buffer);
    }
    text() {
      return Promise.resolve(_decoder.decode(this._bytes()));
    }
    slice(start, end, contentType) {
      const bytes = this._bytes();
      const b = new _Blob([bytes.slice(start, end)]);
      b.type = contentType == null ? "" : String(contentType).toLowerCase();
      return b;
    }
  };

  // browser/DOMException.ts
  var _legacyCodes = {
    IndexSizeError: 1,
    HierarchyRequestError: 3,
    WrongDocumentError: 4,
    InvalidCharacterError: 5,
    NoModificationAllowedError: 7,
    NotFoundError: 8,
    NotSupportedError: 9,
    InUseAttributeError: 10,
    InvalidStateError: 11,
    SyntaxError: 12,
    InvalidModificationError: 13,
    NamespaceError: 14,
    InvalidAccessError: 15,
    SecurityError: 18,
    NetworkError: 19,
    AbortError: 20,
    URLMismatchError: 21,
    QuotaExceededError: 22,
    TimeoutError: 23,
    InvalidNodeTypeError: 24,
    DataCloneError: 25
  };
  var DOMException = class extends Error {
    constructor(message, name) {
      super(message == null ? "" : String(message));
      this.name = name == null ? "Error" : String(name);
      this.code = _legacyCodes[this.name] || 0;
    }
  };
  DOMException.INDEX_SIZE_ERR = 1;
  DOMException.DOMSTRING_SIZE_ERR = 2;
  DOMException.HIERARCHY_REQUEST_ERR = 3;
  DOMException.WRONG_DOCUMENT_ERR = 4;
  DOMException.INVALID_CHARACTER_ERR = 5;
  DOMException.NO_DATA_ALLOWED_ERR = 6;
  DOMException.NO_MODIFICATION_ALLOWED_ERR = 7;
  DOMException.NOT_FOUND_ERR = 8;
  DOMException.NOT_SUPPORTED_ERR = 9;
  DOMException.INUSE_ATTRIBUTE_ERR = 10;
  DOMException.INVALID_STATE_ERR = 11;
  DOMException.SYNTAX_ERR = 12;
  DOMException.INVALID_MODIFICATION_ERR = 13;
  DOMException.NAMESPACE_ERR = 14;
  DOMException.INVALID_ACCESS_ERR = 15;
  DOMException.VALIDATION_ERR = 16;
  DOMException.TYPE_MISMATCH_ERR = 17;
  DOMException.SECURITY_ERR = 18;
  DOMException.NETWORK_ERR = 19;
  DOMException.ABORT_ERR = 20;
  DOMException.URL_MISMATCH_ERR = 21;
  DOMException.QUOTA_EXCEEDED_ERR = 22;
  DOMException.TIMEOUT_ERR = 23;
  DOMException.INVALID_NODE_TYPE_ERR = 24;
  DOMException.DATA_CLONE_ERR = 25;

  // browser/FileList.ts
  var FileList = class {
    constructor() {
      this.length = 0;
    }
    item(_index) {
      return null;
    }
    [Symbol.iterator]() {
      return { next() {
        return { value: void 0, done: true };
      } };
    }
  };

  // browser/base64.ts
  var _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
  function btoa(input) {
    const s = input == null ? "" : String(input);
    let out = "";
    for (let i = 0; i < s.length; ) {
      const c1 = s.charCodeAt(i++);
      if (c1 > 255) throw new Error("The string to be encoded contains characters outside of the Latin1 range.");
      const c2 = i < s.length ? s.charCodeAt(i++) : NaN;
      const c3 = i < s.length ? s.charCodeAt(i++) : NaN;
      if (c2 > 255 || c3 > 255) throw new Error("The string to be encoded contains characters outside of the Latin1 range.");
      const e1 = c1 >> 2;
      const e2 = (c1 & 3) << 4 | (isNaN(c2) ? 0 : c2 >> 4);
      const e3 = isNaN(c2) ? 64 : (c2 & 15) << 2 | (isNaN(c3) ? 0 : c3 >> 6);
      const e4 = isNaN(c3) ? 64 : c3 & 63;
      out += _chars[e1] + _chars[e2] + (e3 === 64 ? "=" : _chars[e3]) + (e4 === 64 ? "=" : _chars[e4]);
    }
    return out;
  }
  function atob(input) {
    const s = (input == null ? "" : String(input)).replace(/[\t\n\f\r ]/g, "");
    if (s.length % 4 === 1) throw new Error("The string to be decoded is not correctly encoded.");
    let out = "";
    let bits = 0;
    let count = 0;
    for (let i = 0; i < s.length; i++) {
      const ch = s[i];
      if (ch === "=") break;
      const v = _chars.indexOf(ch);
      if (v < 0) throw new Error("The string to be decoded is not correctly encoded.");
      bits = bits << 6 | v;
      count += 6;
      if (count >= 8) {
        count -= 8;
        out += String.fromCharCode(bits >> count & 255);
      }
    }
    return out;
  }

  // browser/scroll.ts
  function installScrollApi(global) {
    global.scrollTo = () => {
    };
    global.scrollBy = () => {
    };
    global.scrollByLines = () => {
    };
    global.scrollByPages = () => {
    };
    global.scrollX = 0;
    global.scrollY = 0;
    global.pageXOffset = 0;
    global.pageYOffset = 0;
  }

  // browser/native.ts
  function markNative(fn, name) {
    const label = "function " + name + "() { [native code] }";
    try {
      Object.defineProperty(fn, "toString", {
        value: function() {
          return label;
        },
        writable: true,
        configurable: true,
        enumerable: false
      });
    } catch {
    }
  }
  function markPrototypeNative(ctor) {
    const proto = ctor && ctor.prototype;
    if (!proto) return;
    for (const key of Object.getOwnPropertyNames(proto)) {
      if (key === "constructor") continue;
      const desc = Object.getOwnPropertyDescriptor(proto, key);
      if (desc && typeof desc.value === "function") markNative(desc.value, key);
    }
  }

  // browser/globals.ts
  var doc = new Document(globalThis);
  documentRef.current = doc;
  function installDOM(global) {
    const _windowListeners = {};
    global.document = doc;
    global.window = global;
    global.self = global;
    global.navigator = navigator;
    global.location = createLocation();
    global.history = createHistory();
    global.addEventListener = (t, cb) => addListener(_windowListeners, t, cb);
    global.removeEventListener = (t, cb) => removeListener(_windowListeners, t, cb);
    global.dispatchEvent = (event) => fireEvent(global, _windowListeners, event);
    for (const on of [
      "onresize",
      "onscroll",
      "onload",
      "onerror",
      "onunload",
      "onbeforeunload",
      "onpopstate",
      "onhashchange",
      "onpageshow",
      "onpagehide",
      "onmessage",
      "onoffline",
      "ononline",
      "onfocus",
      "onblur",
      "onorientationchange"
    ]) {
      if (!(on in global)) global[on] = null;
    }
    global.reportError = global.reportError || ((error) => {
      if (typeof global.onerror === "function") {
        try {
          global.onerror(error instanceof Error ? error.message : String(error), "", 0, 0, error);
        } catch {
        }
      }
      reportSwallowed("reportError", error);
    });
    global.getComputedStyle = () => createStyleDeclaration();
    global.getSelection = () => ({
      rangeCount: 0,
      type: "None",
      isCollapsed: true,
      addRange() {
      },
      removeAllRanges() {
      },
      getRangeAt() {
        return null;
      },
      toString() {
        return "";
      }
    });
    installViewport(global);
    global.MutationObserver = function() {
      this.observe = () => {
      };
      this.disconnect = () => {
      };
      this.takeRecords = () => [];
    };
    global.IntersectionObserver = IntersectionObserver;
    global.IntersectionObserverEntry = IntersectionObserverEntry;
    global.ResizeObserver = function() {
      this.observe = () => {
      };
      this.unobserve = () => {
      };
      this.disconnect = () => {
      };
    };
    global.structuredClone = global.structuredClone || ((value) => value == null ? value : JSON.parse(JSON.stringify(value)));
    global.Blob = Blob;
    global.DOMException = global.DOMException || DOMException;
    global.FileList = global.FileList || FileList;
    global.btoa = global.btoa || btoa;
    global.atob = global.atob || atob;
    URL.createObjectURL = URL.createObjectURL || (() => "blob:" + Math.random().toString(36).slice(2));
    URL.revokeObjectURL = URL.revokeObjectURL || (() => {
    });
    global.URL = URL;
    global.URLSearchParams = URLSearchParams;
    global.EventTarget = EventTarget;
    global.Node = Node;
    global.NodeList = NodeList;
    global.Element = Element;
    global.CharacterData = CharacterData;
    global.Document = Document;
    global.DocumentType = DocumentType;
    global.Text = Text;
    global.Comment = Comment;
    global.DocumentFragment = DocumentFragment;
    global.HTMLElement = HTMLElement;
    global.HTMLTemplateElement = HTMLTemplateElement;
    global.CSSTransition = CSSTransition;
    global.Image = HTMLImageElement;
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
    installScrollApi(global);
    for (const ctor of [EventTarget, Node, Element, CharacterData, Document, DocumentFragment, HTMLElement]) {
      markPrototypeNative(ctor);
    }
  }

  // console/constants.ts
  var LEVEL_TRACE = 0;
  var LEVEL_DEBUG = 1;
  var LEVEL_INFO = 2;
  var LEVEL_WARN = 3;
  var LEVEL_ERROR = 4;

  // console/utils.ts
  function formatArgs(args) {
    if (args.length === 0) return "";
    if (args.length === 1) return stringify(args[0]);
    const fmt = stringify(args[0]);
    if (fmt.indexOf("%") < 0) return args.map(stringify).join(" ");
    let out = "";
    let argIdx = 1;
    let i = 0;
    while (i < fmt.length) {
      if (fmt[i] === "%" && i + 1 < fmt.length && argIdx < args.length) {
        const spec = fmt[i + 1];
        if (spec === "s" || spec === "o" || spec === "O") {
          out += stringify(args[argIdx++]);
          i += 2;
          continue;
        }
        if (spec === "d" || spec === "i") {
          out += toInt(args[argIdx++]);
          i += 2;
          continue;
        }
        if (spec === "f") {
          out += toFloat(args[argIdx++]);
          i += 2;
          continue;
        }
        if (spec === "%") {
          out += "%";
          i += 2;
          continue;
        }
      }
      out += fmt[i++];
    }
    while (argIdx < args.length) out += " " + stringify(args[argIdx++]);
    return out;
  }
  function stringify(value) {
    if (value === null) return "null";
    if (value === void 0) return "undefined";
    const type = typeof value;
    if (type === "string") return value;
    if (type === "number" || type === "boolean" || type === "bigint") return String(value);
    if (type === "function" || type === "symbol") return String(value);
    if (value instanceof Error || type === "object" && typeof value.message === "string" && typeof value.stack === "string") {
      const name = typeof value.name === "string" ? value.name : "Error";
      const header = value.message ? name + ": " + value.message : name;
      const stack = typeof value.stack === "string" ? value.stack : "";
      if (!stack) return header;
      return stack.indexOf(header) === 0 ? stack : header + "\n" + stack;
    }
    try {
      return JSON.stringify(value) ?? String(value);
    } catch {
      return String(value);
    }
  }
  function toInt(value) {
    const n = Number(value);
    return Number.isFinite(n) ? Math.trunc(n) : 0;
  }
  function toFloat(value) {
    const n = Number(value);
    return Number.isFinite(n) ? n : 0;
  }

  // console/api.ts
  function installConsole(global) {
    let minLevel = Number.POSITIVE_INFINITY;
    const timers = /* @__PURE__ */ new Map();
    const counters = /* @__PURE__ */ new Map();
    let groupDepth = 0;
    const emit = (level, build) => {
      if (level < minLevel) return;
      const log = global.__crawlerLog;
      if (typeof log !== "function") return;
      const indent = groupDepth > 0 ? " ".repeat(groupDepth * 2) : "";
      log(level, indent + build());
    };
    const label = (args) => args.length > 0 ? stringify(args[0]) : "default";
    global.console = {
      log: (...args) => emit(LEVEL_INFO, () => formatArgs(args)),
      info: (...args) => emit(LEVEL_INFO, () => formatArgs(args)),
      debug: (...args) => emit(LEVEL_DEBUG, () => formatArgs(args)),
      warn: (...args) => emit(LEVEL_WARN, () => formatArgs(args)),
      error: (...args) => emit(LEVEL_ERROR, () => formatArgs(args)),
      trace: (...args) => emit(LEVEL_TRACE, () => formatArgs(args)),
      dir: (...args) => emit(LEVEL_DEBUG, () => formatArgs(args)),
      dirxml: (...args) => emit(LEVEL_DEBUG, () => formatArgs(args)),
      assert: (...args) => {
        if (args.length > 0 && args[0]) return;
        emit(LEVEL_ERROR, () => args.length > 1 ? "Assertion failed: " + formatArgs(args.slice(1)) : "Assertion failed");
      },
      group: (...args) => {
        emit(LEVEL_DEBUG, () => "\u25B6 " + (args.length > 0 ? formatArgs(args) : ""));
        groupDepth++;
      },
      groupCollapsed: (...args) => {
        emit(LEVEL_DEBUG, () => "\u25B6 " + (args.length > 0 ? formatArgs(args) : ""));
        groupDepth++;
      },
      groupEnd: () => {
        if (groupDepth > 0) groupDepth--;
      },
      count: (...args) => {
        const key = label(args);
        const value = (counters.get(key) ?? 0) + 1;
        counters.set(key, value);
        emit(LEVEL_DEBUG, () => `${key}: ${value}`);
      },
      countReset: (...args) => {
        const key = label(args);
        if (!counters.has(key)) emit(LEVEL_WARN, () => `Count for '${key}' does not exist`);
        else counters.set(key, 0);
      },
      time: (...args) => {
        const key = label(args);
        if (timers.has(key)) emit(LEVEL_WARN, () => `Timer '${key}' already exists`);
        else timers.set(key, Date.now());
      },
      timeLog: (...args) => {
        const key = label(args);
        const start = timers.get(key);
        if (start === void 0) {
          emit(LEVEL_WARN, () => `Timer '${key}' does not exist`);
          return;
        }
        const extra = args.length > 1 ? " " + formatArgs(args.slice(1)) : "";
        emit(LEVEL_DEBUG, () => `${key}: ${Date.now() - start}ms${extra}`);
      },
      timeEnd: (...args) => {
        const key = label(args);
        const start = timers.get(key);
        if (start === void 0) {
          emit(LEVEL_WARN, () => `Timer '${key}' does not exist`);
          return;
        }
        timers.delete(key);
        emit(LEVEL_DEBUG, () => `${key}: ${Date.now() - start}ms - timer ended`);
      },
      table: (...args) => emit(LEVEL_DEBUG, () => args.length > 0 ? stringify(args[0]) : "(empty table)"),
      clear: () => {
      }
    };
    global.__crawlerSetLogLevel = (level) => {
      minLevel = level;
    };
  }

  // profiling/domProfiler.ts
  var counts = {};
  var times = {};
  var installed = false;
  var _hostPerf2 = globalThis.Performance;
  var _now = _hostPerf2 && typeof _hostPerf2.now === "function" ? () => _hostPerf2.now() : null;
  var _stack = [];
  function record(label, fn) {
    counts[label] = (counts[label] || 0) + 1;
    if (!_now) return fn();
    const frame = { label, start: _now(), child: 0 };
    _stack.push(frame);
    try {
      return fn();
    } finally {
      _stack.pop();
      const elapsed = _now() - frame.start;
      times[label] = (times[label] || 0) + (elapsed - frame.child);
      const parent = _stack[_stack.length - 1];
      if (parent) parent.child += elapsed;
    }
  }
  function enableDomProfile() {
    if (installed) return;
    installed = true;
    wrapMethods(Node, "Node", ["insertBefore", "removeChild", "cloneNode"]);
    wrapMethods(Element, "Element", [
      "setAttribute",
      "getAttribute",
      "removeAttribute",
      "hasAttribute",
      "querySelector",
      "querySelectorAll",
      "matches",
      "closest",
      "getElementsByTagName",
      "getElementsByClassName",
      "getBoundingClientRect",
      "getClientRects"
    ]);
    wrapMethods(Document, "Document", [
      "createElement",
      "createElementNS",
      "createTextNode",
      "createComment",
      "createDocumentFragment",
      "createRange",
      "getElementById",
      "getElementsByTagName",
      "getElementsByClassName",
      "querySelector",
      "querySelectorAll"
    ]);
    wrapMethods(EventTarget, "EventTarget", ["addEventListener", "removeEventListener", "dispatchEvent"]);
    wrapSetter(Element, "innerHTML", "Element.set innerHTML");
    wrapSetter(Element, "textContent", "Element.set textContent");
    for (const ctor of [Node, Element, Document, EventTarget]) markPrototypeNative(ctor);
  }
  function dumpDomProfile() {
    return installed ? JSON.stringify({ counts, timesMs: _now ? times : null }) : "";
  }
  function wrapMethods(ctor, group, names) {
    const proto = ctor && ctor.prototype;
    if (!proto) return;
    for (const name of names) {
      const orig = proto[name];
      if (typeof orig !== "function") continue;
      const label = group + "." + name;
      proto[name] = function(...args) {
        return record(label, () => orig.apply(this, args));
      };
    }
  }
  function wrapSetter(ctor, prop, label) {
    const proto = ctor && ctor.prototype;
    if (!proto) return;
    const desc = Object.getOwnPropertyDescriptor(proto, prop);
    if (!desc || typeof desc.set !== "function") return;
    const origSet = desc.set;
    desc.set = function(v) {
      record(label, () => {
        origSet.call(this, v);
      });
    };
    Object.defineProperty(proto, prop, desc);
  }

  // crawler/api.ts
  function setCurrentScript(src) {
    if (src == null) {
      doc.currentScript = null;
      return;
    }
    const script = new HTMLScriptElement();
    const s = String(src);
    if (s) script.src = s;
    script.parentNode = doc.head || doc.body || doc.documentElement;
    doc.currentScript = script;
  }
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
  function getBaseHref() {
    if (!doc.documentElement) return "";
    let found = "";
    function walk(n) {
      for (const c of n.childNodes) {
        if (c.nodeType !== 1 /* Element */) continue;
        if (c.localName === "base") {
          const href = c.getAttribute("href");
          if (href) {
            found = href;
            return true;
          }
        }
        if (walk(c)) return true;
      }
      return false;
    }
    walk(doc.documentElement);
    return found;
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
  function countAnchors() {
    if (!doc.documentElement) return 0;
    let count = 0;
    function walk(n) {
      for (const c of n.childNodes) {
        if (c.nodeType !== 1 /* Element */) continue;
        if (c.localName === "a") count++;
        walk(c);
      }
    }
    walk(doc.documentElement);
    return count;
  }
  var _baselineHtml = null;
  var _baselineAnchors = 0;
  function captureBaseline() {
    _baselineHtml = doc.documentElement ? serializeNode(doc.documentElement) : null;
    _baselineAnchors = countAnchors();
  }
  function guardRegression() {
    if (_baselineHtml == null) return -1;
    if (countAnchors() >= _baselineAnchors) return -1;
    parseHTML(doc, _baselineHtml);
    return _baselineAnchors;
  }
  function fireDomContentLoaded() {
    doc.dispatchEvent(new Event("DOMContentLoaded"));
  }
  function installCrawlerApi(global) {
    global.__crawlerSetLocation = (url) => {
      applyUrl(url);
    };
    global.__crawlerSetViewport = (width, height) => {
      setViewport(width, height);
    };
    global.__crawlerSetCurrentScript = (src) => {
      setCurrentScript(src);
    };
    global.__crawlerLoadHtml = (html) => {
      parseHTML(doc, html);
    };
    global.__crawlerCollectScripts = () => JSON.stringify(collectScripts());
    global.__crawlerGetBaseHref = () => getBaseHref();
    global.__crawlerCollectLinks = () => JSON.stringify(collectLinks());
    global.__crawlerPending = () => pendingCount();
    global.__crawlerPump = () => pumpTasks();
    global.__crawlerTakeResources = () => takeResources();
    global.__crawlerPendingResources = () => pendingResourceCount();
    global.__crawlerFireResourceEvent = (id, type) => {
      fireResourceEvent(id, type);
    };
    global.__crawlerFireDomContentLoaded = () => {
      fireDomContentLoaded();
    };
    global.__crawlerSerialize = () => doc.documentElement ? serializeNode(doc.documentElement) : "";
    global.__crawlerCaptureBaseline = () => {
      captureBaseline();
    };
    global.__crawlerGuardRegression = () => guardRegression();
    global.__crawlerEnableWebGl = () => {
      enableWebGl();
    };
    global.__crawlerEnableDomProfile = () => {
      enableDomProfile();
    };
    global.__crawlerDomProfileDump = () => dumpDomProfile();
  }

  // index.ts
  installDOM(globalThis);
  installConsole(globalThis);
  installCrawlerApi(globalThis);
})();
