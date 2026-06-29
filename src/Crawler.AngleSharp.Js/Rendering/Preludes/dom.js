"use strict";
(() => {
  // dom/Node.ts
  var Node = class {
    constructor(type) {
      this.parentNode = null;
      this.childNodes = [];
      this.nodeType = type;
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
  };

  // dom/Text.ts
  var Text = class extends Node {
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
  var Element = class extends Node {
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
    removeEventListener() {
    }
    dispatchEvent() {
      return true;
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
      return super.insertBefore(child, ref);
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
  var Comment = class extends Node {
    constructor(data) {
      super(8 /* Comment */);
      this.data = data == null ? "" : String(data);
    }
  };

  // dom/DocumentFragment.ts
  var DocumentFragment = class extends Node {
    constructor() {
      super(11 /* DocumentFragment */);
    }
  };

  // dom/Document.ts
  var Document = class extends Node {
    constructor(defaultView) {
      super(9 /* Document */);
      this.documentElement = null;
      this.head = null;
      this.body = null;
      this.styleSheets = [];
      this.defaultView = defaultView || null;
    }
    createElement(tag) {
      return new Element(tag);
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

  // browser/globals.ts
  var doc = new Document(globalThis);
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
    installTimerGlobals(global);
  }

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
    return root;
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
  function installCrawlerApi(global) {
    global.__crawlerSetLocation = (url) => {
      applyUrl(url);
    };
    global.__crawlerLoadHtml = (html) => {
      parseHTML(doc, html);
    };
    global.__crawlerCollectScripts = () => JSON.stringify(collectScripts());
    global.__crawlerPending = () => pendingCount();
    global.__crawlerPump = () => pumpTasks();
    global.__crawlerSerialize = () => doc.documentElement ? serializeNode(doc.documentElement) : "";
  }

  // index.ts
  installDOM(globalThis);
  installCrawlerApi(globalThis);
})();
