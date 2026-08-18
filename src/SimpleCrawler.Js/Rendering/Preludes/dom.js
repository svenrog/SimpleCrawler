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
  var _Node = class _Node extends EventTarget {
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
    // Every node answers the document's base URL; Document overrides this with the computation. Bundles
    // resolve their own asset URLs against `node.baseURI` (a web component reading it off itself), where
    // undefined is a throw inside the component's constructor rather than a missed lookup.
    get baseURI() {
      const doc2 = this.ownerDocument;
      return doc2 ? doc2.baseURI : "";
    }
    // Node's, not Element's — `document.contains(el)` is the guard a deferred-script loader runs before it
    // activates anything, and a document that cannot answer it loses every script behind the loader.
    contains(n) {
      let cur = n;
      while (cur) {
        if (cur === this) return true;
        cur = cur.parentNode;
      }
      return false;
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
    // Every node answers this, not only elements: it is Node.prototype's in a browser, and a text node's
    // parentElement is what a text-measuring or highlight library reads to find the box it sits in. An
    // accessibility overlay copies the descriptor off Node.prototype to wrap it, and finding none there
    // threw at defineProperty rather than skipping the wrap.
    get parentElement() {
      const p = this.parentNode;
      return p && p.nodeType === 1 /* Element */ ? p : null;
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
    // Read before it is called — a consent banner swaps its markup in with
    // `host.replaceChildren.apply(host, Array.from(tmp.childNodes))` — so the gap is a throw inside that
    // banner's init, not a skipped update.
    replaceChildren(...nodes) {
      for (const c of this.childNodes.slice()) this.removeChild(c);
      for (const n of nodes) this.appendChild(asNode(n));
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
        for (const name of names) if (a.getAttributeInternal(name) !== b.getAttributeInternal(name)) return false;
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
    // Focus/tab-order libraries sort nodes with an `a.compareDocumentPosition(b)` comparator; without it the
    // sort throws inside a useMemo and the render subtree fails. Returns the bitmask describing `other`'s
    // position relative to `this`.
    compareDocumentPosition(other) {
      if (other === this) return 0;
      const thisChain = [];
      for (let n = this; n; n = n.parentNode) thisChain.push(n);
      const otherChain = [];
      for (let n = other; n; n = n.parentNode) otherChain.push(n);
      if (thisChain[thisChain.length - 1] !== otherChain[otherChain.length - 1])
        return _Node.DOCUMENT_POSITION_DISCONNECTED | _Node.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC | _Node.DOCUMENT_POSITION_FOLLOWING;
      if (otherChain.indexOf(this) >= 0)
        return _Node.DOCUMENT_POSITION_CONTAINED_BY | _Node.DOCUMENT_POSITION_FOLLOWING;
      if (thisChain.indexOf(other) >= 0)
        return _Node.DOCUMENT_POSITION_CONTAINS | _Node.DOCUMENT_POSITION_PRECEDING;
      thisChain.reverse();
      otherChain.reverse();
      let i = 0;
      while (thisChain[i] === otherChain[i]) i++;
      const kids = thisChain[i - 1].childNodes;
      return kids.indexOf(thisChain[i]) < kids.indexOf(otherChain[i]) ? _Node.DOCUMENT_POSITION_FOLLOWING : _Node.DOCUMENT_POSITION_PRECEDING;
    }
  };
  _Node.DOCUMENT_POSITION_DISCONNECTED = 1;
  _Node.DOCUMENT_POSITION_PRECEDING = 2;
  _Node.DOCUMENT_POSITION_FOLLOWING = 4;
  _Node.DOCUMENT_POSITION_CONTAINS = 8;
  _Node.DOCUMENT_POSITION_CONTAINED_BY = 16;
  _Node.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC = 32;
  var Node = _Node;
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
    // Text carried this alone, which left a comment's textContent undefined — and hydration finds its
    // boundaries by walking childNodes for `8 === n.nodeType && n.textContent.trim() === marker`.
    get textContent() {
      return this.data;
    }
    set textContent(v) {
      this.data = v == null ? "" : String(v);
    }
    get length() {
      return this.data.length;
    }
    appendData(v) {
      this.data += v == null ? "" : String(v);
    }
    substringData(offset, count) {
      const start = Math.max(0, Number(offset) || 0);
      return this.data.slice(start, start + Math.max(0, Number(count) || 0));
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
    // Splits at `offset`, keeping the head and returning the tail as the next sibling. Hydration walks a
    // server-rendered text run and splits it where the client tree expects a boundary; without this the
    // reconciler throws mid-commit and the subtree it was mounting is lost.
    splitText(offset) {
      const at = Math.max(0, Math.min(Number(offset) || 0, this.data.length));
      const tail = new _Text(this.data.slice(at));
      this.data = this.data.slice(0, at);
      if (this.parentNode) this.parentNode.insertBefore(tail, this.nextSibling);
      return tail;
    }
    _shallowClone() {
      return new _Text(this.data);
    }
  };

  // css/CSSStyleDeclaration.ts
  function canonical(name) {
    const text = String(name);
    if (text.slice(0, 2) === "--") return text;
    return text.replace(/[A-Z]/g, (c) => "-" + c.toLowerCase());
  }
  function declarations(text) {
    const out = [];
    let depth = 0;
    let quote = "";
    let start = 0;
    for (let i = 0; i < text.length; i++) {
      const ch = text[i];
      if (quote) {
        if (ch === quote) quote = "";
        continue;
      }
      if (ch === '"' || ch === "'") quote = ch;
      else if (ch === "(") depth++;
      else if (ch === ")") {
        if (depth > 0) depth--;
      } else if (ch === ";" && depth === 0) {
        out.push(text.slice(start, i));
        start = i + 1;
      }
    }
    out.push(text.slice(start));
    return out;
  }
  function parseCss(text, store) {
    for (const part of declarations(String(text))) {
      const idx = part.indexOf(":");
      if (idx > 0) store[canonical(part.slice(0, idx).trim())] = part.slice(idx + 1).trim();
    }
  }
  function createStyleDeclaration(owner) {
    const store = {};
    let attribute = null;
    const serialize = () => {
      const out = [];
      for (const p in store) if (Object.prototype.hasOwnProperty.call(store, p)) out.push(p + ": " + store[p]);
      return out.join("; ");
    };
    const pull = () => {
      if (!owner) return;
      const current = owner.getAttributeInternal("style");
      if (current === attribute) return;
      for (const p in store) if (Object.prototype.hasOwnProperty.call(store, p)) delete store[p];
      if (current) parseCss(current, store);
      attribute = current;
    };
    const push = () => {
      if (!owner) return;
      const text = serialize();
      attribute = text || null;
      if (text) owner.setAttributeInternal("style", text);
      else owner.removeAttributeInternal("style");
    };
    const handler = {
      get: (_t, k) => {
        if (k === "setProperty") return (n, v2) => {
          pull();
          store[canonical(n)] = String(v2);
          push();
        };
        if (k === "removeProperty") return (n) => {
          pull();
          delete store[canonical(n)];
          push();
        };
        if (k === "getPropertyValue") return (n) => {
          pull();
          return store[canonical(n)] || "";
        };
        if (k === "getPropertyPriority") return () => "";
        if (k === "item") return (i) => {
          pull();
          return Object.keys(store)[i] || "";
        };
        if (k === "length") {
          pull();
          return Object.keys(store).length;
        }
        if (k === "cssText") {
          pull();
          return serialize();
        }
        if (k === "_store") {
          pull();
          return store;
        }
        if (typeof k !== "string") return void 0;
        pull();
        const v = store[canonical(k)];
        return v != null ? v : "";
      },
      set: (_t, k, v) => {
        if (typeof k !== "string") return true;
        pull();
        if (k === "cssText") {
          for (const p in store) if (Object.prototype.hasOwnProperty.call(store, p)) delete store[p];
          if (v) parseCss(v, store);
        } else if (v == null || v === "") {
          delete store[canonical(k)];
        } else {
          store[canonical(k)] = String(v);
        }
        push();
        return true;
      },
      // A real style object answers `in` for every CSS property it supports, set or not, and for both the
      // unprefixed and vendor-prefixed spellings. Without a has trap `in` falls through to the bare target
      // and is false for everything — including properties this shim itself just stored — which contradicts
      // the get trap above. The gap is not cosmetic: a prefix probe of the shape
      // `"transform" in style || "WebkitTransform" in style || ...` concludes the property is unsupported,
      // and libraries then use that null as a property name rather than taking a fallback (GSAP's
      // _checkPropPrefix does exactly this, and every subsequent transform read throws). This answers true
      // for unknown names too, where a real browser answers false; that mirrors the get trap, which already
      // returns "" for any key rather than shipping a CSS-property table, and feature probes ask about real
      // (possibly prefixed/future) properties, never deliberate non-properties.
      has: () => true,
      ownKeys: () => {
        pull();
        return Object.keys(store);
      },
      getOwnPropertyDescriptor: (_t, k) => {
        pull();
        if (typeof k !== "string") return void 0;
        const key = canonical(k);
        if (!Object.prototype.hasOwnProperty.call(store, key)) return void 0;
        return { value: store[key], writable: true, enumerable: true, configurable: true };
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

  // selector/querySelector.ts
  var _cache = /* @__PURE__ */ new Map();
  function querySelectorAll(root, sel) {
    const out = new NodeList();
    const list = parseOrThrow(String(sel));
    const scope = root.nodeType === 1 /* Element */ ? root : null;
    const documentElement = root.documentElement;
    if (documentElement) walk(documentElement);
    else for (const c of root.childNodes) walk(c);
    return out;
    function walk(n) {
      if (n.nodeType === 1 /* Element */ && matchesAny(n, list, scope)) out.push(n);
      for (const c of n.childNodes) walk(c);
    }
  }
  function matchesSelector(el, selector) {
    return matchesAny(el, parseOrThrow(String(selector)), null);
  }
  function parseOrThrow(selector) {
    const list = parseList(selector);
    if (!list) {
      throw new DOMException(
        "Failed to execute 'querySelectorAll': '" + selector + "' is not a valid selector.",
        "SyntaxError"
      );
    }
    return list;
  }
  function matchesAny(el, list, scope) {
    for (const complex of list) {
      if (matchComplex(el, complex, complex.length - 1, scope)) return true;
    }
    return false;
  }
  function matchComplex(el, steps, index, scope) {
    if (!matchCompound(el, steps[index].compound, scope)) return false;
    if (index === 0) return true;
    const combinator = steps[index].combinator;
    if (combinator === ">") {
      const p = el.parentNode;
      return !!p && p.nodeType === 1 /* Element */ && matchComplex(p, steps, index - 1, scope);
    }
    if (combinator === "+") {
      const s = previousElement(el);
      return !!s && matchComplex(s, steps, index - 1, scope);
    }
    if (combinator === "~") {
      for (let s = previousElement(el); s; s = previousElement(s)) {
        if (matchComplex(s, steps, index - 1, scope)) return true;
      }
      return false;
    }
    for (let p = el.parentNode; p && p.nodeType === 1 /* Element */; p = p.parentNode) {
      if (matchComplex(p, steps, index - 1, scope)) return true;
    }
    return false;
  }
  function matchCompound(el, compound, scope) {
    for (const simple of compound) {
      if (!matchSimple(el, simple, scope)) return false;
    }
    return compound.length > 0;
  }
  function matchSimple(el, simple, scope) {
    switch (simple.kind) {
      case "universal":
        return true;
      case "type":
        return el.localName === simple.name;
      case "id":
        return el.getAttributeInternal("id") === simple.name;
      case "class":
        return hasClass(el, simple.name);
      case "attr":
        return matchesAttr(el, simple);
      default:
        return matchesPseudo(el, simple, scope);
    }
  }
  function matchesPseudo(el, simple, scope) {
    switch (simple.name) {
      case "not":
        return !matchesAny(el, simple.list, scope);
      case "is":
      case "where":
      case "matches":
      case "-webkit-any":
      case "-moz-any":
        return matchesAny(el, simple.list, scope);
      case "has":
        return matchesHas(el, simple.list);
      case "scope":
        return scope ? el === scope : el === rootElement(el);
      case "root":
        return el === rootElement(el);
      case "empty":
        return isEmpty(el);
      case "first-child":
        return childIndex(el, false) === 1;
      case "last-child":
        return childIndex(el, true) === 1;
      case "only-child":
        return childIndex(el, false) === 1 && childIndex(el, true) === 1;
      case "first-of-type":
        return typeIndex(el, false) === 1;
      case "last-of-type":
        return typeIndex(el, true) === 1;
      case "only-of-type":
        return typeIndex(el, false) === 1 && typeIndex(el, true) === 1;
      case "nth-child":
        return matchesStep(childIndex(el, false), simple.step) && matchesOf(el, simple, scope);
      case "nth-last-child":
        return matchesStep(childIndex(el, true), simple.step) && matchesOf(el, simple, scope);
      case "nth-of-type":
        return matchesStep(typeIndex(el, false), simple.step);
      case "nth-last-of-type":
        return matchesStep(typeIndex(el, true), simple.step);
      case "checked":
        return el.hasAttribute("checked") || el.hasAttribute("selected") || el.checked === true;
      case "disabled":
        return el.hasAttribute("disabled");
      case "enabled":
        return !el.hasAttribute("disabled");
      case "required":
        return el.hasAttribute("required");
      case "optional":
        return !el.hasAttribute("required");
      case "read-only":
        return el.hasAttribute("readonly") || el.hasAttribute("disabled");
      case "read-write":
        return !el.hasAttribute("readonly") && !el.hasAttribute("disabled");
      case "any-link":
      case "link":
        return (el.localName === "a" || el.localName === "area") && el.hasAttribute("href");
      case "defined":
        return true;
      case "open":
        return el.hasAttribute("open");
      // The rest of _validPseudos: valid CSS this render can never be in — no pointer, no focus, no
      // navigation, no user input — and the pseudo-elements, which match no element anywhere.
      default:
        return false;
    }
  }
  var _validPseudos = /* @__PURE__ */ new Set([
    // answered by matchesPseudo
    "not",
    "is",
    "where",
    "matches",
    "-webkit-any",
    "-moz-any",
    "has",
    "scope",
    "root",
    "empty",
    "first-child",
    "last-child",
    "only-child",
    "first-of-type",
    "last-of-type",
    "only-of-type",
    "nth-child",
    "nth-last-child",
    "nth-of-type",
    "nth-last-of-type",
    "checked",
    "disabled",
    "enabled",
    "required",
    "optional",
    "read-only",
    "read-write",
    "any-link",
    "link",
    "defined",
    "open",
    // state this render is never in
    "hover",
    "active",
    "focus",
    "focus-visible",
    "focus-within",
    "target",
    "target-within",
    "visited",
    "local-link",
    "current",
    "past",
    "future",
    "playing",
    "paused",
    "seeking",
    "buffering",
    "stalled",
    "muted",
    "volume-locked",
    "fullscreen",
    "modal",
    "popover-open",
    "picture-in-picture",
    "autofill",
    "user-invalid",
    "user-valid",
    "valid",
    "invalid",
    "in-range",
    "out-of-range",
    "placeholder-shown",
    "blank",
    "default",
    "indeterminate",
    "closed",
    // valid, but describing a tree or a locale this engine does not model
    "lang",
    "dir",
    "host",
    "host-context",
    "nth-col",
    "nth-last-col",
    "state",
    "popover",
    "has-slotted",
    // pseudo-elements, including the four the CSS2 single-colon syntax still allows
    "before",
    "after",
    "first-line",
    "first-letter",
    "backdrop",
    "placeholder",
    "marker",
    "selection",
    "file-selector-button",
    "grammar-error",
    "spelling-error",
    "target-text",
    "highlight",
    "part",
    "slotted",
    "cue",
    "cue-region",
    "view-transition",
    "details-content",
    "__never"
  ]);
  function matchesOf(el, simple, scope) {
    return !simple.list || matchesAny(el, simple.list, scope);
  }
  function matchesHas(el, list) {
    for (const complex of list) {
      const relation = complex[0].combinator;
      if (relation === "+" || relation === "~") {
        for (let s = nextElement(el); s; s = nextElement(s)) {
          if (matchComplex(s, complex, complex.length - 1, el)) return true;
          if (relation === "+") break;
        }
        continue;
      }
      if (descendantMatches(el, complex, el)) return true;
    }
    return false;
  }
  function descendantMatches(el, complex, scope) {
    for (const c of el.childNodes) {
      if (c.nodeType !== 1 /* Element */) continue;
      if (matchComplex(c, complex, complex.length - 1, scope)) return true;
      if (descendantMatches(c, complex, scope)) return true;
    }
    return false;
  }
  function rootElement(el) {
    let cur = el;
    while (cur.parentNode && cur.parentNode.nodeType === 1 /* Element */) cur = cur.parentNode;
    return cur;
  }
  function isEmpty(el) {
    for (const c of el.childNodes) {
      if (c.nodeType === 1 /* Element */) return false;
      if (c.nodeType === 3 /* Text */ && String(c.data || "").length > 0) return false;
    }
    return true;
  }
  function previousElement(el) {
    let n = el.previousSibling;
    while (n && n.nodeType !== 1 /* Element */) n = n.previousSibling;
    return n || null;
  }
  function nextElement(el) {
    let n = el.nextSibling;
    while (n && n.nodeType !== 1 /* Element */) n = n.nextSibling;
    return n || null;
  }
  function childIndex(el, fromEnd) {
    const p = el.parentNode;
    if (!p) return 0;
    let index = 0;
    let found = 0;
    for (const c of p.childNodes) {
      if (c.nodeType !== 1 /* Element */) continue;
      index++;
      if (c === el) found = index;
    }
    return found === 0 ? 0 : fromEnd ? index - found + 1 : found;
  }
  function typeIndex(el, fromEnd) {
    const p = el.parentNode;
    if (!p) return 0;
    let index = 0;
    let found = 0;
    for (const c of p.childNodes) {
      if (c.nodeType !== 1 /* Element */ || c.localName !== el.localName) continue;
      index++;
      if (c === el) found = index;
    }
    return found === 0 ? 0 : fromEnd ? index - found + 1 : found;
  }
  function matchesStep(position, step) {
    if (position === 0) return false;
    if (step.a === 0) return position === step.b;
    const n = (position - step.b) / step.a;
    return n >= 0 && Number.isInteger(n);
  }
  function hasClass(el, name) {
    const cls = el.getAttributeInternal("class");
    if (!cls) return false;
    return cls.split(/\s+/).indexOf(name) >= 0;
  }
  function matchesAttr(el, simple) {
    if (!el.hasAttribute(simple.name)) return false;
    if (!simple.op) return true;
    const expected = simple.insensitive ? simple.value.toLowerCase() : simple.value;
    const raw = el.getAttributeInternal(simple.name) ?? "";
    const actual = simple.insensitive ? raw.toLowerCase() : raw;
    switch (simple.op) {
      case "=":
        return actual === expected;
      case "~=":
        return expected !== "" && actual.split(/\s+/).indexOf(expected) >= 0;
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
  function parseList(selector) {
    const cached = _cache.get(selector);
    if (cached !== void 0) return cached;
    let parsed;
    try {
      parsed = parseSelectorList(selector);
    } catch {
      parsed = null;
    }
    if (_cache.size < 4096) _cache.set(selector, parsed);
    return parsed;
  }
  function parseSelectorList(selector) {
    if (/\\[\r\n\f]/.test(selector)) return null;
    const out = [];
    for (const part of splitTopLevel(selector, ",")) {
      const complex = parseComplex(part);
      if (!complex) return null;
      out.push(complex);
    }
    return out.length ? out : null;
  }
  function splitTopLevel(input, sep) {
    const out = [];
    let depth = 0;
    let quote = "";
    let start = 0;
    for (let i = 0; i < input.length; i++) {
      const ch = input[i];
      if (ch === "\\") {
        i++;
        continue;
      }
      if (quote) {
        if (ch === quote) quote = "";
        continue;
      }
      if (ch === '"' || ch === "'") quote = ch;
      else if (ch === "[" || ch === "(") depth++;
      else if (ch === "]" || ch === ")") {
        if (depth > 0) depth--;
      } else if (depth === 0 && ch === sep) {
        out.push(input.slice(start, i));
        start = i + 1;
      }
    }
    out.push(input.slice(start));
    return out;
  }
  function parseComplex(part) {
    const s = part.trim();
    if (!s) return null;
    const steps = [];
    let combinator = "";
    let i = 0;
    while (i < s.length) {
      let spaced = false;
      while (i < s.length && /\s/.test(s[i])) {
        i++;
        spaced = true;
      }
      if (i >= s.length) break;
      const ch = s[i];
      if (ch === ">" || ch === "+" || ch === "~") {
        combinator = ch;
        i++;
        continue;
      }
      if (spaced && combinator === "" && steps.length) combinator = " ";
      const end = compoundEnd(s, i);
      const compound = parseCompound(s.slice(i, end));
      if (!compound) return null;
      steps.push({ compound, combinator: steps.length === 0 ? combinator : combinator || " " });
      combinator = "";
      i = end;
    }
    return steps.length ? steps : null;
  }
  function compoundEnd(s, from) {
    let depth = 0;
    let quote = "";
    for (let i = from; i < s.length; i++) {
      const ch = s[i];
      if (ch === "\\") {
        i++;
        continue;
      }
      if (quote) {
        if (ch === quote) quote = "";
        continue;
      }
      if (ch === '"' || ch === "'") quote = ch;
      else if (ch === "[" || ch === "(") depth++;
      else if (ch === "]" || ch === ")") {
        if (depth > 0) depth--;
      } else if (depth === 0 && (/\s/.test(ch) || ch === ">" || ch === "+" || ch === "~")) return i;
    }
    return s.length;
  }
  function parseCompound(text) {
    const out = [];
    let i = 0;
    while (i < text.length) {
      const ch = text[i];
      if (ch === "*") {
        out.push({ kind: "universal", name: "*" });
        i++;
        continue;
      }
      if (ch === "#" || ch === ".") {
        const start2 = ++i;
        i = identEnd(text, i);
        if (i === start2 || !isIdent(text.slice(start2, i))) return null;
        out.push({ kind: ch === "#" ? "id" : "class", name: unescapeIdent(text.slice(start2, i)) });
        continue;
      }
      if (ch === "[") {
        const close = closingIndex(text, i, "[", "]");
        if (close < 0) return null;
        const attr = parseAttr(text.slice(i + 1, close));
        if (!attr) return null;
        out.push(attr);
        i = close + 1;
        continue;
      }
      if (ch === ":") {
        let start2 = i + 1;
        const doubled = text[start2] === ":";
        if (doubled) start2++;
        let end = identEnd(text, start2);
        if (end === start2) return null;
        const name = text.slice(start2, end).toLowerCase();
        let arg = "";
        if (text[end] === "(") {
          const close = closingIndex(text, end, "(", ")");
          if (close < 0) return null;
          arg = text.slice(end + 1, close);
          end = close + 1;
        }
        i = end;
        const pseudo = parsePseudo(doubled ? "__never" : name, arg);
        if (!pseudo) return null;
        out.push(pseudo);
        continue;
      }
      const start = i;
      i = identEnd(text, i);
      if (i === start || !isIdent(text.slice(start, i))) return null;
      out.push({ kind: "type", name: unescapeIdent(text.slice(start, i)).toLowerCase() });
    }
    return out.length ? out : null;
  }
  function parsePseudo(name, arg) {
    if (name === "not" || name === "is" || name === "where" || name === "matches" || name === "has" || name === "-webkit-any" || name === "-moz-any") {
      const list = parseSelectorList(arg);
      if (!list) return null;
      return { kind: "pseudo", name, list };
    }
    if (name === "nth-child" || name === "nth-last-child" || name === "nth-of-type" || name === "nth-last-of-type") {
      const parts = splitTopLevel(arg, " ").map((p) => p.trim()).filter((p) => p.length > 0);
      const step = parseStep(parts[0] || "");
      if (!step) return null;
      const of = parts.length >= 3 && parts[1].toLowerCase() === "of" ? parseSelectorList(parts.slice(2).join(" ")) : null;
      return of ? { kind: "pseudo", name, step, list: of } : { kind: "pseudo", name, step };
    }
    if (!_validPseudos.has(name) && name.charAt(0) !== "-") return null;
    return { kind: "pseudo", name, value: arg };
  }
  function parseStep(text) {
    const s = text.trim().toLowerCase().replace(/\s+/g, "");
    if (s === "odd") return { a: 2, b: 1 };
    if (s === "even") return { a: 2, b: 0 };
    const m = s.match(/^([+-]?\d*)n([+-]\d+)?$/);
    if (m) {
      const a = m[1] === "" || m[1] === "+" ? 1 : m[1] === "-" ? -1 : Number(m[1]);
      return { a, b: m[2] ? Number(m[2]) : 0 };
    }
    if (/^[+-]?\d+$/.test(s)) return { a: 0, b: Number(s) };
    return null;
  }
  function parseAttr(text) {
    const m = text.match(/^\s*([^\s~^$*|=\]]+)\s*(?:([~^$*|]?=)\s*(?:"([^"]*)"|'([^']*)'|([^\s\]]*))\s*([iIsS])?\s*)?$/);
    if (!m || !isIdent(m[1])) return null;
    return {
      kind: "attr",
      name: unescapeIdent(m[1]),
      op: m[2],
      value: m[3] ?? m[4] ?? m[5] ?? "",
      insensitive: !!m[6] && m[6].toLowerCase() === "i"
    };
  }
  function closingIndex(text, from, open, close) {
    let depth = 0;
    let quote = "";
    for (let i = from; i < text.length; i++) {
      const ch = text[i];
      if (ch === "\\") {
        i++;
        continue;
      }
      if (quote) {
        if (ch === quote) quote = "";
        continue;
      }
      if (ch === '"' || ch === "'") quote = ch;
      else if (ch === open) depth++;
      else if (ch === close) {
        depth--;
        if (depth === 0) return i;
      }
    }
    return -1;
  }
  function identEnd(text, from) {
    let i = from;
    while (i < text.length) {
      const ch = text[i];
      if (ch === "\\") {
        i += 2;
        continue;
      }
      if (ch === "#" || ch === "." || ch === "[" || ch === "]" || ch === ":" || ch === "(" || ch === ")" || ch === "*" || ch === "," || ch === ">" || ch === "+" || ch === "~" || /\s/.test(ch)) break;
      i++;
    }
    return Math.min(i, text.length);
  }
  function isIdent(raw) {
    return /^(?:[\w\u00A0-\uFFFF|-]|\\[\s\S])+$/.test(raw);
  }
  function unescapeIdent(text) {
    return text.indexOf("\\") < 0 ? text : text.replace(/\\(.)/g, "$1");
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
    const raw = RAWTEXT_ELEMENTS[node.localName];
    let s = "";
    for (const c of node.childNodes) s += raw && c.nodeType === 3 /* Text */ ? c.data : serializeNode(c);
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
      s += " " + k + '="' + escapeAttr(el.getAttributeInternal(k)) + '"';
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
    // The pre-constructor spelling, still how a polyfill built on document.createEvent names its event —
    // and it names it *after* creating it, so the type cannot be readonly.
    initEvent(type, bubbles, cancelable) {
      this.type = String(type);
      this.bubbles = !!bubbles;
      this.cancelable = !!cancelable;
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
  var _parserInserted = /* @__PURE__ */ new WeakSet();
  function markParserInserted(node) {
    _parserInserted.add(node);
  }
  function clearParserInserted(node) {
    _parserInserted.delete(node);
    const kids = node.childNodes;
    if (kids) for (let i = 0; i < kids.length; i++) clearParserInserted(kids[i]);
  }
  var _counter = 0;
  var _pending = [];
  var _byId = /* @__PURE__ */ new Map();
  var _seen = /* @__PURE__ */ new WeakSet();
  var runnableTypes = ["", "text/javascript", "module", "application/javascript"];
  function registerResource(node) {
    const tag = node.localName;
    if (tag !== "script" && tag !== "link") return;
    if (tag === "script" && _parserInserted.has(node)) return;
    if (tag === "script" && !node.getAttributeInternal("src")) {
      const type = (node.getAttributeInternal("type") || "").trim().toLowerCase();
      if (runnableTypes.indexOf(type) === -1) return;
      if (!String(node.textContent || "")) return;
    }
    if (_seen.has(node)) return;
    _seen.add(node);
    const id = ++_counter;
    _pending.push({ id, node });
    _byId.set(id, node);
  }
  function takeResources() {
    if (!_pending.length) return "";
    const batch = _pending.splice(0, _pending.length);
    return JSON.stringify(batch.map((r) => ({
      id: r.id,
      tag: r.node.localName,
      src: r.node.getAttributeInternal("src") || "",
      type: r.node.getAttributeInternal("type") || "",
      text: r.node.getAttributeInternal("src") ? "" : String(r.node.textContent || "")
    })));
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

  // dom/DOMTokenList.ts
  var _lists = /* @__PURE__ */ new WeakMap();
  var DOMTokenList = class {
    constructor(owner, attribute) {
      this._owner = owner;
      this._attribute = attribute;
    }
    _read() {
      const value = this._owner.getAttributeInternal(this._attribute);
      return (value || "").split(/\s+/).filter(Boolean);
    }
    // Through setAttribute, not the attribute map underneath it: a custom element observing "class" is
    // notified of a classList write exactly as a browser notifies it, which the old direct write skipped.
    _write(tokens) {
      this._owner.setAttributeInternal(this._attribute, tokens.join(" "));
    }
    add(...names) {
      const tokens = this._read();
      for (const name of names) if (tokens.indexOf(name) < 0) tokens.push(name);
      this._write(tokens);
    }
    remove(...names) {
      this._write(this._read().filter((x) => names.indexOf(x) < 0));
    }
    toggle(name, force) {
      const has = this._read().indexOf(name) >= 0;
      const next = force === void 0 ? !has : force;
      if (next && !has) this._write([...this._read(), name]);
      else if (!next && has) this._write(this._read().filter((x) => x !== name));
      return next;
    }
    replace(oldName, newName) {
      const tokens = this._read();
      const at = tokens.indexOf(oldName);
      if (at < 0) return false;
      tokens[at] = newName;
      this._write(tokens);
      return true;
    }
    contains(name) {
      return this._read().indexOf(name) >= 0;
    }
    item(index) {
      return this._read()[index] ?? null;
    }
    forEach(callback) {
      const tokens = this._read();
      for (let i = 0; i < tokens.length; i++) callback(tokens[i], i, this);
    }
    // Every token list is a live class attribute here, and no conditional-feature attribute (rel, sandbox)
    // is modelled, so nothing is supported — the spec answer for a list with no defined token set.
    supports(_token) {
      return false;
    }
    get length() {
      return this._read().length;
    }
    get value() {
      return this._read().join(" ");
    }
    set value(v) {
      this._owner.setAttributeInternal(this._attribute, v == null ? "" : String(v));
    }
    keys() {
      return this._read().keys();
    }
    values() {
      return this._read().values();
    }
    entries() {
      return this._read().entries();
    }
    [Symbol.iterator]() {
      return this._read()[Symbol.iterator]();
    }
    toString() {
      return this._read().join(" ");
    }
  };
  function classListFor(owner) {
    let list = _lists.get(owner);
    if (!list) {
      list = new DOMTokenList(owner, "class");
      _lists.set(owner, list);
    }
    return list;
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

  // dom/HTMLCollection.ts
  var HTMLCollection = class extends Array {
    item(index) {
      return this[index] ?? null;
    }
    // Browsers key this on id first, then on the name attribute for the elements that carry one.
    namedItem(name) {
      const key = String(name);
      for (const node of this) {
        const el = node;
        if (!el || typeof el.getAttributeInternal !== "function") continue;
        if (el.getAttributeInternal("id") === key || el.getAttributeInternal("name") === key) return node;
      }
      return null;
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

  // dom/ShadowRoot.ts
  var ShadowRoot = class extends DocumentFragment {
    constructor() {
      super(...arguments);
      this.host = null;
      this.mode = "open";
    }
    get nodeName() {
      return "#document-fragment";
    }
  };

  // dom/Element.ts
  function attrKey(name) {
    return typeof name === "string" ? name : String(name);
  }
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
      this.shadowRoot = null;
      this.attrs = /* @__PURE__ */ new Map();
      this.cachedInnerHTML = null;
      this._sheet = null;
      this.localName = String(tag).toLowerCase();
      this.tagName = this.localName.toUpperCase();
      this.nodeName = this.tagName;
      this.namespaceURI = ns || "http://www.w3.org/1999/xhtml";
      this.style = createStyleDeclaration(this);
      hideOwnFields(this);
    }
    // Shadow DOM lives on Element, not on HTMLElement: a page that tests support reads
    // `Element.prototype.attachShadow` (or `'attachShadow' in Element.prototype`) rather than calling it on
    // an instance, and a prototype that does not carry it reads as a browser without shadow DOM.
    attachShadow(init) {
      if (this.shadowRoot) return this.shadowRoot;
      const root = new ShadowRoot();
      root.host = this;
      root.mode = init && init.mode ? init.mode : "open";
      this.shadowRoot = root;
      return root;
    }
    // The attribute steps a browser runs inside the platform, where page code cannot reach them. Every
    // reflected IDL property (el.src, el.href, …) goes through these rather than through the public methods
    // below: a consent blocker that wraps Element.prototype.setAttribute and, inside its wrapper, assigns the
    // property it is guarding re-enters its own wrapper otherwise, and that recursion spends the whole stack
    // before the page has run. A browser is immune because its setter never calls the method.
    setAttributeInternal(name, value) {
      this.attrs.set(attrKey(name), value == null ? "" : String(value));
    }
    getAttributeInternal(name) {
      const key = attrKey(name);
      return this.attrs.has(key) ? this.attrs.get(key) : null;
    }
    removeAttributeInternal(name) {
      this.attrs.delete(attrKey(name));
    }
    setAttribute(name, value) {
      this.setAttributeInternal(name, value);
    }
    setAttributeNS(_ns, name, value) {
      this.setAttributeInternal(name, value);
    }
    getAttribute(name) {
      return this.getAttributeInternal(name);
    }
    removeAttribute(name) {
      this.removeAttributeInternal(name);
    }
    removeAttributeNS(_ns, name) {
      this.attrs.delete(attrKey(name));
    }
    hasAttribute(name) {
      return this.attrs.has(attrKey(name));
    }
    toggleAttribute(name, force) {
      const key = attrKey(name);
      const present = this.attrs.has(key);
      const add = force === void 0 ? !present : force;
      if (add) {
        if (!present) this.attrs.set(key, "");
        return true;
      }
      this.attrs.delete(key);
      return false;
    }
    getAttributeNames() {
      return Array.from(this.attrs.keys());
    }
    // A live NamedNodeMap-ish view: custom-element upgrade code walks `el.attributes` by index reading
    // `.length`/`[i].name`/`[i].value`, and React's singleton-attribute teardown does
    // `for (c = el.attributes; c.length;) el.removeAttributeNode(c[0])` — that loop only terminates if
    // `.length` is read live off the current attribute set, not snapshotted once when `.attributes` is accessed.
    // A NamedNodeMap also exposes its attributes as named properties, which is how a jQuery-era feature
    // detection reads one back after setting it (`div.attributes[name].expando`); it dereferences the
    // result, so an undefined there throws during the library's own init.
    get attributes() {
      const el = this;
      return new Proxy({}, {
        get(_t, prop) {
          if (prop === "length") return el.attrs.size;
          if (prop === "item") return (i) => nthAttrNode(el.attrs, i, el);
          if (prop === "getNamedItem") return (name) => el.getAttributeNode(name);
          if (prop === Symbol.iterator) {
            return function* () {
              for (const name of Array.from(el.attrs.keys())) yield el.getAttributeNode(name);
            };
          }
          if (typeof prop !== "string") return void 0;
          if (/^\d+$/.test(prop)) return nthAttrNode(el.attrs, Number(prop), el);
          return el.attrs.has(prop) ? attrNode(prop, el.attrs.get(prop), el) : void 0;
        },
        // Array.prototype.slice.call(el.attributes) — how a widget copies an element's attributes onto
        // another — asks whether each index is present before reading it, and a bare target answers no,
        // leaving an array of holes. The index and key traps have to agree with the getter.
        has(_t, prop) {
          if (prop === "length" || prop === "item" || prop === "getNamedItem" || prop === Symbol.iterator) return true;
          if (typeof prop !== "string") return false;
          return /^\d+$/.test(prop) ? Number(prop) < el.attrs.size : el.attrs.has(prop);
        },
        ownKeys() {
          const keys = [];
          for (let i = 0; i < el.attrs.size; i++) keys.push(String(i));
          for (const name of el.attrs.keys()) keys.push(name);
          keys.push("length");
          return keys;
        },
        getOwnPropertyDescriptor(_t, prop) {
          if (prop === "length") return { value: el.attrs.size, writable: false, enumerable: false, configurable: true };
          if (typeof prop !== "string") return void 0;
          const value = /^\d+$/.test(prop) ? nthAttrNode(el.attrs, Number(prop), el) : el.attrs.has(prop) ? attrNode(prop, el.attrs.get(prop), el) : void 0;
          return value === void 0 ? void 0 : { value, writable: false, enumerable: true, configurable: true };
        }
      });
    }
    getAttributeNode(name) {
      const key = attrKey(name);
      return this.attrs.has(key) ? attrNode(key, this.attrs.get(key), this) : null;
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
      const out = new HTMLCollection();
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
    // (cancel/play/pause, onfinish, currentTime) and animation-sequencing code awaits `.finished`/`.ready`
    // then calls `.commitStyles()`, so a missing member throws inside the effect that a finite offsetWidth now
    // lets run. Hand back an inert, already-settled Animation. Each call gets its own resolved promises.
    animate() {
      return {
        currentTime: 0,
        startTime: null,
        playState: "finished",
        playbackRate: 1,
        pending: false,
        effect: null,
        timeline: null,
        id: "",
        finished: Promise.resolve(),
        ready: Promise.resolve(),
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
        persist() {
        },
        commitStyles() {
        },
        updatePlaybackRate() {
        },
        addEventListener() {
        },
        removeEventListener() {
        }
      };
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
    get lang() {
      return this.attrs.get("lang") || "";
    }
    set lang(v) {
      this.attrs.set("lang", String(v));
    }
    get classList() {
      return classListFor(this);
    }
    get children() {
      const out = new HTMLCollection();
      for (const n of this.childNodes) if (n.nodeType === 1 /* Element */) out.push(n);
      return out;
    }
    get childElementCount() {
      return this.children.length;
    }
    // Element-only traversal. Slider/drag libraries step through slides via nextElementSibling and cache
    // the track's firstElementChild; a missing accessor returns undefined where they expect an
    // element-or-null, so the next `.removeAttribute`/`.classList` call throws instead of skipping.
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
      const out = new HTMLCollection();
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
      if (parse) for (const node of parse(html, this.localName)) this.appendChild(node);
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
    // A browser's innerText is the *rendered* text, which a layout-free render cannot compute; the
    // concatenated text is the closest honest answer. It has to exist at all because page code reads
    // `.length`/`.trim()` straight off the lookup, and undefined there throws instead of measuring nothing.
    get innerText() {
      return textOf(this);
    }
    set innerText(v) {
      this.textContent = v;
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
      const nodes = parse ? parse(html, parent.localName) : [];
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
    // The three adjacent-insertion methods, which a widget uses in place of innerHTML precisely because it
    // must not disturb the siblings already there. Called bare, so absence is a TypeError that costs the
    // whole script rather than one insertion. An unknown position is a no-op here, where a browser throws:
    // the render has nothing to gain from ending a script over a misspelt argument.
    insertAdjacentElement(position, element) {
      this.insertAdjacent(position, [element]);
      return element;
    }
    insertAdjacentText(position, text) {
      this.insertAdjacent(position, [new Text(text == null ? "" : String(text))]);
    }
    insertAdjacentHTML(position, html) {
      const parse = parserRef.parseFragment;
      this.insertAdjacent(position, parse ? parse(html == null ? "" : String(html)) : []);
    }
    insertAdjacent(position, nodes) {
      const where = String(position).toLowerCase();
      const parent = this.parentNode;
      if (where === "beforeend") {
        for (const node of nodes) this.appendChild(node);
      } else if (where === "afterbegin") {
        const first = this.childNodes[0] || null;
        for (const node of nodes) this.insertBefore(node, first);
      } else if (where === "beforebegin" && parent) {
        for (const node of nodes) parent.insertBefore(node, this);
      } else if (where === "afterend" && parent) {
        const next = this.nextSibling;
        for (const node of nodes) parent.insertBefore(node, next);
      }
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
      // Constraint Validation API. Frameworks grab a form control ref and call setCustomValidity during
      // render, so the methods must exist; they no-op and report valid.
      this.willValidate = true;
      this.validationMessage = "";
      hideOwnFields(this);
      const target = customElements.takeUpgradeTarget();
      if (target) return target;
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
    // Overrides the internal steps rather than the public method, so an observed attribute reports its change
    // however it was set — through setAttribute, or through the reflected property that bypasses it.
    setAttributeInternal(name, value) {
      const observed = this.constructor.observedAttributes;
      const tracked = Array.isArray(observed) && observed.indexOf(name) >= 0;
      const old = tracked ? this.getAttributeInternal(name) : null;
      super.setAttributeInternal(name, value);
      if (tracked && typeof this.attributeChangedCallback === "function") {
        this.attributeChangedCallback(name, old, this.getAttributeInternal(name));
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
    const bm = b.match(/^([a-zA-Z][\w+.-]*:)\/\/([^/?#]*)([^?#]*)/) || [];
    const scheme = bm[1] || "http:";
    const origin = bm[2] ? scheme + "//" + bm[2] : "http://localhost";
    if (input.slice(0, 2) === "//") return scheme + input;
    if (input.charAt(0) === "/") return origin + input;
    if (input.charAt(0) === "#" || input.charAt(0) === "?") return origin + (bm[3] || "/") + input;
    const dir = (bm[3] || "/").replace(/[^/]*$/, "");
    return origin + dir + input;
  }
  function applyUrl(u) {
    try {
      const abs = resolveUrl(u);
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
      // Set by the URL that owns this list, so a mutation reaches the URL's own serialization. The spec calls
      // these the update steps; without them `u.searchParams.set(k, v)` writes into an object nothing reads,
      // and the request the page then makes carries the query it had before.
      this.onchange = null;
      this.reset(init);
    }
    get size() {
      return this.pairs.length;
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
      const key = String(k);
      const out = [];
      let replaced = false;
      for (const pair of this.pairs) {
        if (pair[0] !== key) out.push(pair);
        else if (!replaced) {
          out.push([key, String(v)]);
          replaced = true;
        }
      }
      if (!replaced) out.push([key, String(v)]);
      this.pairs = out;
      this.changed();
    }
    append(k, v) {
      this.pairs.push([String(k), String(v)]);
      this.changed();
    }
    delete(k) {
      const key = String(k);
      this.pairs = this.pairs.filter((p) => p[0] !== key);
      this.changed();
    }
    sort() {
      this.pairs.sort((a, b) => a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0);
      this.changed();
    }
    forEach(cb, thisArg) {
      this.pairs.slice().forEach((p) => cb.call(thisArg, p[1], p[0]));
    }
    entries() {
      let i = 0;
      const snapshot = this.pairs.slice();
      const it = {
        next: () => i < snapshot.length ? { value: snapshot[i++], done: false } : { value: void 0, done: true },
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
      return this.pairs.map((p) => encode(p[0]) + "=" + encode(p[1])).join("&");
    }
    [Symbol.iterator]() {
      return this.entries();
    }
    // Replaces the whole list without notifying — the owning URL calls this when its own query is assigned.
    reset(init) {
      this.pairs = parseInit(init);
    }
    observe(onchange) {
      this.onchange = onchange;
    }
    changed() {
      if (this.onchange) this.onchange(this.toString());
    }
  };
  function parseInit(init) {
    if (init == null) return [];
    if (init instanceof URLSearchParams) return Array.from(init);
    if (typeof init === "string") return parseQuery(init);
    if (typeof init[Symbol.iterator] === "function") {
      const out = [];
      for (const entry of init) {
        if (entry == null) continue;
        out.push([String(entry[0]), String(entry[1] == null ? "" : entry[1])]);
      }
      return out;
    }
    if (typeof init === "object") {
      return Object.keys(init).map(
        (k) => [k, String(init[k] == null ? "" : init[k])]
      );
    }
    return parseQuery(String(init));
  }
  function parseQuery(text) {
    const src = text.charAt(0) === "?" ? text.slice(1) : text;
    const out = [];
    if (!src) return out;
    for (const part of src.split("&")) {
      if (!part) continue;
      const i = part.indexOf("=");
      out.push(i < 0 ? [decode(part), ""] : [decode(part.slice(0, i)), decode(part.slice(i + 1))]);
    }
    return out;
  }
  function decode(text) {
    try {
      return decodeURIComponent(text.replace(/\+/g, " "));
    } catch {
      return text;
    }
  }
  function encode(text) {
    return encodeURIComponent(text).replace(/%20/g, "+");
  }

  // url/URL.ts
  var URL = class {
    constructor(url, base) {
      this._scheme = "";
      this._host = "";
      this._path = "/";
      this._query = "";
      this._fragment = "";
      this.assign(resolveUrl(url, base));
      this.searchParams = new URLSearchParams(this._query);
      this.searchParams.observe((serialized) => {
        this._query = serialized ? "?" + serialized : "";
      });
    }
    get href() {
      return this._scheme + "//" + this._host + this._path + this._query + this._fragment;
    }
    set href(value) {
      this.assign(resolveUrl(value));
      this.searchParams.reset(this._query);
    }
    get protocol() {
      return this._scheme;
    }
    set protocol(value) {
      const scheme = String(value ?? "").replace(/:*$/, "");
      if (/^[a-zA-Z][\w+.-]*$/.test(scheme)) this._scheme = scheme + ":";
    }
    get host() {
      return this._host;
    }
    set host(value) {
      const host = String(value ?? "");
      if (host) this._host = host;
    }
    get hostname() {
      return this._host.split(":")[0];
    }
    set hostname(value) {
      const name = String(value ?? "");
      if (name) this._host = this.port ? name + ":" + this.port : name;
    }
    get port() {
      return this._host.split(":")[1] || "";
    }
    set port(value) {
      const port = String(value ?? "");
      this._host = port ? this.hostname + ":" + port : this.hostname;
    }
    get pathname() {
      return this._path;
    }
    set pathname(value) {
      const path = String(value ?? "");
      this._path = path.charAt(0) === "/" ? path : "/" + path;
    }
    get search() {
      return this._query;
    }
    set search(value) {
      const query = String(value ?? "");
      this._query = !query ? "" : query.charAt(0) === "?" ? query : "?" + query;
      this.searchParams.reset(this._query);
    }
    get hash() {
      return this._fragment;
    }
    set hash(value) {
      const hash = String(value ?? "");
      this._fragment = !hash ? "" : hash.charAt(0) === "#" ? hash : "#" + hash;
    }
    get origin() {
      return this._scheme + "//" + this._host;
    }
    toString() {
      return this.href;
    }
    toJSON() {
      return this.href;
    }
    assign(abs) {
      const m = abs.match(/^([a-zA-Z][\w+.-]*:)\/\/([^/?#]*)([^?#]*)(\?[^#]*)?(#.*)?$/) || [];
      this._scheme = m[1] || "";
      this._host = m[2] || "";
      this._path = m[3] || "/";
      this._query = m[4] || "";
      this._fragment = m[5] || "";
    }
  };

  // dom/HTMLAnchorElement.ts
  var HTMLAnchorElement = class extends HTMLElement {
    constructor() {
      super("a");
    }
    get href() {
      const raw = this.getAttributeInternal("href");
      if (raw == null) return "";
      try {
        return new URL(raw).href;
      } catch {
        return raw;
      }
    }
    set href(value) {
      this.setAttributeInternal("href", value == null ? "" : String(value));
    }
    resolved() {
      const raw = this.getAttributeInternal("href");
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
      const raw = this.getAttributeInternal("src");
      if (raw == null) return "";
      try {
        return new URL(raw).href;
      } catch {
        return raw;
      }
    }
    set src(value) {
      this.setAttributeInternal("src", value == null ? "" : String(value));
    }
    get type() {
      return this.getAttributeInternal("type") || "";
    }
    set type(value) {
      this.setAttributeInternal("type", value == null ? "" : String(value));
    }
    // The source of an inline script, as the IDL property rather than the node's text. jQuery's globalEval
    // and every tag manager that injects a snippet assign this one, and an element that treats it as an
    // ordinary expando keeps an empty textContent — so the script that was just written has nothing to run.
    get text() {
      return String(this.textContent ?? "");
    }
    set text(value) {
      this.textContent = value == null ? "" : String(value);
    }
    // The module-support feature test, and the only one a page runs against a *created* element rather than
    // the window: `'noModule' in document.createElement('script')`. An element that does not carry it reads
    // as a pre-2018 browser, and a bundle that branches on it can replace document.body with an
    // "unsupported browser" page — which costs every script that runs after it the whole DOM, not just its
    // own globals. The renderer runs ES modules, so the honest answer is that the property exists.
    get noModule() {
      return this.hasAttribute("nomodule");
    }
    set noModule(value) {
      if (value) this.setAttributeInternal("nomodule", "");
      else this.removeAttributeInternal("nomodule");
    }
  };

  // dom/HTMLLinkElement.ts
  var HTMLLinkElement = class extends HTMLElement {
    constructor() {
      super("link");
    }
    get href() {
      return this.getAttributeInternal("href") || "";
    }
    set href(value) {
      this.setAttributeInternal("href", value == null ? "" : String(value));
    }
    get rel() {
      return this.getAttributeInternal("rel") || "";
    }
    set rel(value) {
      this.setAttributeInternal("rel", value == null ? "" : String(value));
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
      const v = this.getAttributeInternal("value");
      return v != null ? v : this.textContent;
    }
    set value(v) {
      this.setAttributeInternal("value", v == null ? "" : String(v));
    }
  };

  // dom/HTMLImageElement.ts
  var HTMLImageElement = class extends HTMLElement {
    constructor() {
      super("img");
    }
    get alt() {
      return this.getAttributeInternal("alt") || "";
    }
    set alt(value) {
      this.setAttributeInternal("alt", value == null ? "" : String(value));
    }
    get src() {
      return this.getAttributeInternal("src") || "";
    }
    set src(value) {
      this.setAttributeInternal("src", value == null ? "" : String(value));
    }
  };

  // dom/HTMLIFrameElement.ts
  var HTMLIFrameElement = class extends HTMLElement {
    constructor() {
      super("iframe");
    }
    get src() {
      return this.getAttributeInternal("src") || "";
    }
    set src(value) {
      this.setAttributeInternal("src", value == null ? "" : String(value));
    }
    get contentWindow() {
      return this._contentWindow || this._openFrame().window;
    }
    get contentDocument() {
      return this._contentDocument || this._openFrame().document;
    }
    _openFrame() {
      const doc2 = documentRef.current.implementation.createHTMLDocument("");
      const own = {
        document: doc2,
        postMessage() {
        },
        close() {
        },
        focus() {
        },
        blur() {
        }
      };
      const realm = globalThis;
      const win = new Proxy(own, {
        get: (target, prop) => prop in target ? target[prop] : realm[prop],
        has: (target, prop) => prop in target || prop in realm
      });
      doc2.defaultView = win;
      Object.defineProperty(this, "_contentWindow", { value: win, enumerable: false });
      Object.defineProperty(this, "_contentDocument", { value: doc2, enumerable: false });
      return { window: win, document: doc2 };
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
      return this.getAttributeInternal("src") || "";
    }
    set src(value) {
      this.setAttributeInternal("src", value == null ? "" : String(value));
    }
    get muted() {
      return this.hasAttribute("muted");
    }
    set muted(value) {
      if (value) this.setAttributeInternal("muted", "");
      else this.removeAttributeInternal("muted");
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
      return this.getAttributeInternal("poster") || "";
    }
    set poster(value) {
      this.setAttributeInternal("poster", value == null ? "" : String(value));
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
      if (value) this.setAttributeInternal("open", "");
      else this.removeAttributeInternal("open");
    }
    show() {
      this.setAttributeInternal("open", "");
    }
    showModal() {
      this.setAttributeInternal("open", "");
    }
    close(returnValue) {
      this.removeAttributeInternal("open");
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
      const v = parseInt(this.getAttributeInternal("width") || "", 10);
      return isNaN(v) ? 300 : v;
    }
    set width(value) {
      this.setAttributeInternal("width", String(value == null ? 0 : value));
    }
    get height() {
      const v = parseInt(this.getAttributeInternal("height") || "", 10);
      return isNaN(v) ? 150 : v;
    }
    set height(value) {
      this.setAttributeInternal("height", String(value == null ? 0 : value));
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

  // dom/HTMLMetaElement.ts
  var HTMLMetaElement = class extends HTMLElement {
    constructor() {
      super("meta");
    }
    get content() {
      return this.getAttributeInternal("content") || "";
    }
    set content(value) {
      this.setAttributeInternal("content", value == null ? "" : String(value));
    }
    get name() {
      return this.getAttributeInternal("name") || "";
    }
    set name(value) {
      this.setAttributeInternal("name", value == null ? "" : String(value));
    }
    get httpEquiv() {
      return this.getAttributeInternal("http-equiv") || "";
    }
    set httpEquiv(value) {
      this.setAttributeInternal("http-equiv", value == null ? "" : String(value));
    }
  };

  // dom/HTMLInputElement.ts
  var HTMLInputElement = class extends HTMLElement {
    constructor(tag) {
      super(tag || "input");
      this._value = null;
      this._checked = null;
    }
    get value() {
      if (this._value !== null) return this._value;
      return this.getAttributeInternal("value") ?? "";
    }
    set value(v) {
      this._value = v == null ? "" : String(v);
    }
    get defaultValue() {
      return this.getAttributeInternal("value") ?? "";
    }
    set defaultValue(v) {
      this.setAttributeInternal("value", v == null ? "" : String(v));
    }
    get checked() {
      return this._checked !== null ? this._checked : this.hasAttribute("checked");
    }
    set checked(v) {
      this._checked = !!v;
    }
    get defaultChecked() {
      return this.hasAttribute("checked");
    }
    set defaultChecked(v) {
      if (v) this.setAttributeInternal("checked", "");
      else this.removeAttributeInternal("checked");
    }
    get type() {
      return (this.getAttributeInternal("type") ?? "text").toLowerCase();
    }
    set type(v) {
      this.setAttributeInternal("type", v == null ? "" : String(v));
    }
    get name() {
      return this.getAttributeInternal("name") ?? "";
    }
    set name(v) {
      this.setAttributeInternal("name", v == null ? "" : String(v));
    }
    get disabled() {
      return this.hasAttribute("disabled");
    }
    set disabled(v) {
      if (v) this.setAttributeInternal("disabled", "");
      else this.removeAttributeInternal("disabled");
    }
    // The form this control submits with: the one its owner attribute names, else the nearest ancestor form.
    // Page code retargets a search box with `input.form.action = …`, which needs the element, not null.
    get form() {
      const owner = this.getAttributeInternal("form");
      if (owner) return documentRef.current ? documentRef.current.getElementById(owner) : null;
      for (let n = this.parentNode; n; n = n.parentNode) {
        if (n.localName === "form") return n;
      }
      return null;
    }
    get placeholder() {
      return this.getAttributeInternal("placeholder") ?? "";
    }
    set placeholder(v) {
      this.setAttributeInternal("placeholder", v == null ? "" : String(v));
    }
    select() {
    }
    setSelectionRange() {
    }
  };

  // dom/HTMLTextAreaElement.ts
  var HTMLTextAreaElement = class extends HTMLInputElement {
    constructor() {
      super("textarea");
    }
    get value() {
      const own = super.value;
      return own !== "" ? own : this.textContent;
    }
    set value(v) {
      super.value = v;
    }
  };

  // dom/HTMLFormElement.ts
  var HTMLFormElement = class extends HTMLElement {
    constructor() {
      super("form");
    }
    get action() {
      const raw = this.getAttributeInternal("action");
      if (raw == null) return "";
      try {
        return new URL(raw).href;
      } catch {
        return raw;
      }
    }
    set action(value) {
      this.setAttributeInternal("action", value == null ? "" : String(value));
    }
    get method() {
      return (this.getAttributeInternal("method") ?? "get").toLowerCase();
    }
    set method(value) {
      this.setAttributeInternal("method", value == null ? "" : String(value));
    }
    get name() {
      return this.getAttributeInternal("name") ?? "";
    }
    set name(value) {
      this.setAttributeInternal("name", value == null ? "" : String(value));
    }
    get elements() {
      return this.querySelectorAll("input, select, textarea, button");
    }
    // Nothing navigates in a single-pass render; the methods exist so a submit handler's own call does not
    // throw partway through the work it does around it.
    submit() {
    }
    requestSubmit() {
    }
    reset() {
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
    canvas: () => new HTMLCanvasElement(),
    meta: () => new HTMLMetaElement(),
    input: () => new HTMLInputElement(),
    textarea: () => new HTMLTextAreaElement(),
    form: () => new HTMLFormElement()
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
  var _impliedEnd = {
    li: { li: 1 },
    dt: { dt: 1, dd: 1 },
    dd: { dt: 1, dd: 1 },
    option: { option: 1 },
    optgroup: { option: 1, optgroup: 1 },
    tr: { td: 1, th: 1, tr: 1 },
    td: { td: 1, th: 1 },
    th: { td: 1, th: 1 },
    tbody: { td: 1, th: 1, tr: 1, tbody: 1, thead: 1, tfoot: 1 },
    thead: { td: 1, th: 1, tr: 1, tbody: 1, thead: 1, tfoot: 1 },
    tfoot: { td: 1, th: 1, tr: 1, tbody: 1, thead: 1, tfoot: 1 },
    rt: { rt: 1, rp: 1 },
    rp: { rt: 1, rp: 1 }
  };
  var _closesParagraph = {
    address: 1,
    article: 1,
    aside: 1,
    blockquote: 1,
    center: 1,
    details: 1,
    dialog: 1,
    dir: 1,
    div: 1,
    dl: 1,
    fieldset: 1,
    figcaption: 1,
    figure: 1,
    footer: 1,
    form: 1,
    h1: 1,
    h2: 1,
    h3: 1,
    h4: 1,
    h5: 1,
    h6: 1,
    header: 1,
    hgroup: 1,
    hr: 1,
    li: 1,
    main: 1,
    menu: 1,
    nav: 1,
    ol: 1,
    p: 1,
    pre: 1,
    search: 1,
    section: 1,
    summary: 1,
    table: 1,
    ul: 1
  };
  var _impliedRowGroup = { tbody: 1, thead: 1, tfoot: 1 };
  function createLocalElement(tag) {
    const factory = reflectedElementFactories[tag];
    const el = factory ? factory() : new HTMLElement(tag);
    if (tag === "script") markParserInserted(el);
    return el;
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
    function closeImplied(tag) {
      const ends = _impliedEnd[tag];
      while (open.length > 1) {
        const current = open[open.length - 1].localName;
        if (ends && ends[current]) {
          open.pop();
          continue;
        }
        if (current === "p" && _closesParagraph[tag]) {
          open.pop();
          continue;
        }
        return;
      }
    }
    function openImplied(tag) {
      if (tag === "tr" || tag === "td" || tag === "th") {
        if (open[open.length - 1].localName === "table") {
          const group = createLocalElement("tbody");
          attachChild(open[open.length - 1], group);
          open.push(group);
        }
      }
      if ((tag === "td" || tag === "th") && _impliedRowGroup[open[open.length - 1].localName]) {
        const row = createLocalElement("tr");
        attachChild(open[open.length - 1], row);
        open.push(row);
      }
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
          el.setAttributeInternal(an, val);
        }
        i = j;
        if (structural) continue;
        closeImplied(tag);
        openImplied(tag);
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
  function parseFragment(html, context) {
    const scratch = {};
    parseHTML(scratch, html);
    const host = context === "html" ? scratch.documentElement : scratch.body;
    const kids = host.childNodes.slice();
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

  // dom/NodeFilter.ts
  var NodeFilter = {
    FILTER_ACCEPT: 1,
    FILTER_REJECT: 2,
    FILTER_SKIP: 3,
    SHOW_ALL: 4294967295,
    SHOW_ELEMENT: 1,
    SHOW_ATTRIBUTE: 2,
    SHOW_TEXT: 4,
    SHOW_CDATA_SECTION: 8,
    SHOW_ENTITY_REFERENCE: 16,
    SHOW_ENTITY: 32,
    SHOW_PROCESSING_INSTRUCTION: 64,
    SHOW_COMMENT: 128,
    SHOW_DOCUMENT: 256,
    SHOW_DOCUMENT_TYPE: 512,
    SHOW_DOCUMENT_FRAGMENT: 1024,
    SHOW_NOTATION: 2048
  };
  function accepts(node, whatToShow, filter) {
    if ((1 << node.nodeType - 1 & whatToShow) === 0) return NodeFilter.FILTER_SKIP;
    if (!filter) return NodeFilter.FILTER_ACCEPT;
    const accept = typeof filter === "function" ? filter : filter.acceptNode;
    if (typeof accept !== "function") return NodeFilter.FILTER_ACCEPT;
    const verdict = accept.call(filter, node);
    return verdict === NodeFilter.FILTER_REJECT || verdict === NodeFilter.FILTER_SKIP ? verdict : NodeFilter.FILTER_ACCEPT;
  }
  function nextInOrder(node, root, skipChildren) {
    if (!skipChildren && node.childNodes.length) return node.childNodes[0];
    let current = node;
    while (current && current !== root) {
      const sibling = current.nextSibling;
      if (sibling) return sibling;
      current = current.parentNode;
    }
    return null;
  }
  function previousInOrder(node, root) {
    if (node === root) return null;
    let previous = node.previousSibling;
    if (!previous) return node.parentNode;
    while (previous.childNodes.length) previous = previous.childNodes[previous.childNodes.length - 1];
    return previous;
  }

  // dom/TreeWalker.ts
  var TreeWalker = class {
    constructor(root, whatToShow, filter) {
      this.root = root;
      this.whatToShow = whatToShow === void 0 ? NodeFilter.SHOW_ALL : whatToShow >>> 0;
      this.filter = filter || null;
      this.currentNode = root;
    }
    _accepts(node) {
      return accepts(node, this.whatToShow, this.filter);
    }
    parentNode() {
      let node = this.currentNode;
      while (node && node !== this.root) {
        node = node.parentNode;
        if (node && this._accepts(node) === NodeFilter.FILTER_ACCEPT) {
          this.currentNode = node;
          return node;
        }
      }
      return null;
    }
    firstChild() {
      return this._child(true);
    }
    lastChild() {
      return this._child(false);
    }
    nextSibling() {
      return this._sibling(true);
    }
    previousSibling() {
      return this._sibling(false);
    }
    nextNode() {
      let node = this.currentNode;
      let verdict = NodeFilter.FILTER_ACCEPT;
      while (true) {
        node = nextInOrder(node, this.root, verdict === NodeFilter.FILTER_REJECT);
        if (!node) return null;
        verdict = this._accepts(node);
        if (verdict === NodeFilter.FILTER_ACCEPT) {
          this.currentNode = node;
          return node;
        }
      }
    }
    previousNode() {
      let node = this.currentNode;
      while (true) {
        node = previousInOrder(node, this.root);
        if (!node || node === this.root) return null;
        if (this._accepts(node) === NodeFilter.FILTER_ACCEPT) {
          this.currentNode = node;
          return node;
        }
      }
    }
    // A SKIP verdict looks through the node to its own children; a REJECT verdict abandons the subtree.
    _child(forward) {
      const kids = this.currentNode.childNodes;
      for (let i = 0; i < kids.length; i++) {
        const node = kids[forward ? i : kids.length - 1 - i];
        const verdict = this._accepts(node);
        if (verdict === NodeFilter.FILTER_ACCEPT) {
          this.currentNode = node;
          return node;
        }
        if (verdict === NodeFilter.FILTER_SKIP) {
          const saved = this.currentNode;
          this.currentNode = node;
          const descendant = this._child(forward);
          if (descendant) return descendant;
          this.currentNode = saved;
        }
      }
      return null;
    }
    _sibling(forward) {
      let node = this.currentNode;
      while (node && node !== this.root) {
        let sibling = forward ? node.nextSibling : node.previousSibling;
        while (sibling) {
          const verdict = this._accepts(sibling);
          if (verdict === NodeFilter.FILTER_ACCEPT) {
            this.currentNode = sibling;
            return sibling;
          }
          if (verdict === NodeFilter.FILTER_SKIP && sibling.childNodes.length) {
            const saved = this.currentNode;
            this.currentNode = sibling;
            const descendant = this._child(forward);
            if (descendant) return descendant;
            this.currentNode = saved;
          }
          sibling = forward ? sibling.nextSibling : sibling.previousSibling;
        }
        node = node.parentNode;
      }
      return null;
    }
  };

  // dom/NodeIterator.ts
  var NodeIterator = class {
    constructor(root, whatToShow, filter) {
      this.root = root;
      this.whatToShow = whatToShow === void 0 ? NodeFilter.SHOW_ALL : whatToShow >>> 0;
      this.filter = filter || null;
      this.referenceNode = root;
      this.pointerBeforeReferenceNode = true;
    }
    nextNode() {
      let node = this.referenceNode;
      let before = this.pointerBeforeReferenceNode;
      while (true) {
        if (before) before = false;
        else {
          node = nextInOrder(node, this.root, false);
          if (!node) return null;
        }
        if (accepts(node, this.whatToShow, this.filter) === NodeFilter.FILTER_ACCEPT) {
          this.referenceNode = node;
          this.pointerBeforeReferenceNode = false;
          return node;
        }
      }
    }
    previousNode() {
      let node = this.referenceNode;
      let before = this.pointerBeforeReferenceNode;
      while (true) {
        if (!before) before = true;
        else {
          node = previousInOrder(node, this.root);
          if (!node) return null;
        }
        if (accepts(node, this.whatToShow, this.filter) === NodeFilter.FILTER_ACCEPT) {
          this.referenceNode = node;
          this.pointerBeforeReferenceNode = true;
          return node;
        }
      }
    }
    // Detaching a NodeIterator has been a no-op since DOM4; callers written against the old API still call it.
    detach() {
    }
  };

  // browser/fonts.ts
  function createFontFaceSet() {
    const set = /* @__PURE__ */ new Set();
    set.ready = Promise.resolve(set);
    set.status = "loaded";
    set.check = () => true;
    set.load = () => Promise.resolve([]);
    set.addEventListener = () => {
    };
    set.removeEventListener = () => {
    };
    set.onloading = null;
    set.onloadingdone = null;
    set.onloadingerror = null;
    return set;
  }

  // browser/CustomEvent.ts
  var CustomEvent = class extends Event {
    constructor(type, init) {
      super(type, init);
      this.detail = init && init.detail !== void 0 ? init.detail : null;
    }
    initCustomEvent(type, bubbles, cancelable, detail) {
      this.initEvent(type, bubbles, cancelable);
      this.detail = detail === void 0 ? null : detail;
    }
  };

  // dom/Document.ts
  function withinViewport(x, y) {
    const px = Number(x);
    const py = Number(y);
    return px >= 0 && py >= 0 && px <= viewportWidth() && py <= viewportHeight();
  }
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
      this.visibilityState = "visible";
      this.hidden = false;
      this._cookies = /* @__PURE__ */ new Map();
      this._fonts = null;
      this.defaultView = defaultView || null;
      hideOwnFields(this);
    }
    // Browsers expose document.location as an alias of window.location; scripts (analytics, Clerk's CDN
    // loader) read document.location.protocol/href, which threw on undefined when only window.location existed.
    get location() {
      return this.defaultView ? this.defaultView.location : null;
    }
    // The page's own address, read as a string by consent/analytics code that never touches location
    // (`document.URL.indexOf(...)`, `new URL(document.documentURI)`). Both alias location.href here: this
    // render performs no navigation, so there is no history entry for them to diverge over.
    get URL() {
      const loc = this.location;
      return loc && loc.href ? String(loc.href) : "";
    }
    get documentURI() {
      return this.URL;
    }
    // The base against which the document's relative URLs resolve: the first <base href>, resolved against
    // the page URL, else the page URL itself. Node.baseURI delegates here for every node in the tree.
    get baseURI() {
      const base = this.querySelector("base");
      const href = base ? base.getAttributeInternal("href") : null;
      return href ? resolveUrl(href, this.URL) : this.URL;
    }
    // The <title> element's text, which analytics and consent code reads as a string on every page
    // (`document.title.replace(...)`, `title.split("|")`). Absent, it answers undefined and the read after it
    // throws. The setter creates the element when the document has none, exactly as a browser does.
    get title() {
      const el = this.querySelector("title");
      return el ? String(el.textContent ?? "") : "";
    }
    set title(value) {
      const text = value == null ? "" : String(value);
      let el = this.querySelector("title");
      if (!el) {
        if (!this.head) return;
        el = this.createElement("title");
        this.head.appendChild(el);
      }
      el.textContent = text;
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
    // In the HTML namespace this is createElement with the namespace spelled out, and it must answer the
    // same classes: Cloudflare's Rocket Loader rebuilds every deferred script with
    // createElementNS(script.namespaceURI, "script") and then assigns .src/.textContent, so a plain Element
    // here means the page's own scripts are rebuilt into elements that reflect nothing and never load —
    // on a Rocket Loader site that is the whole page.
    createElementNS(ns, tag) {
      if (!ns || ns === "http://www.w3.org/1999/xhtml") return this.createElement(tag);
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
    createTreeWalker(root, whatToShow, filter) {
      return new TreeWalker(root, whatToShow, filter);
    }
    createNodeIterator(root, whatToShow, filter) {
      return new NodeIterator(root, whatToShow, filter);
    }
    // Nothing here is written during parsing — the host parses the whole shell before any script runs — so a
    // write lands at the end of the body, which is where a trailing loader script's own write would have gone.
    // A written <script src> is a real appended resource: the drain loop fetches and runs it like any other.
    // Deliberately not the browser's post-load behaviour, which implicitly calls document.open() and wipes the
    // page: a bundle that writes after load would take the whole render's content with it.
    write(...parts) {
      const target = this.body || this.documentElement;
      const parse = parserRef.parseFragment;
      if (!target || !parse) return;
      const html = parts.map((p) => p == null ? "" : String(p)).join("");
      for (const node of parse(html)) {
        clearParserInserted(node);
        target.appendChild(node);
      }
    }
    writeln(...parts) {
      this.write(parts.map((p) => p == null ? "" : String(p)).join("") + "\n");
    }
    // A single-pass render has no parser to suspend and no stream to reopen; the pair exists so a loader that
    // brackets its write() with them doesn't throw on the way in or out.
    open() {
      return this;
    }
    close() {
    }
    getElementById(id) {
      return walkFind(this.documentElement, (e) => e.getAttributeInternal("id") === id);
    }
    // The root element is in scope for the document's own getElementsBy* — unlike an element's, which search
    // strictly below themselves. A browser answers document.getElementsByTagName("html") with the root, and
    // jQuery resolves a tag-only $("html") through exactly that call: an empty list there is undefined where
    // the caller expects an element, so `$("html").attr("lang").indexOf(...)` throws inside a CMS bundle's
    // init and costs every global it would have registered.
    getElementsByTagName(tag) {
      const name = String(tag).toLowerCase();
      const out = new HTMLCollection();
      if (this.documentElement) {
        if (name === "*" || this.documentElement.localName === name) out.push(this.documentElement);
        collectByTag(this.documentElement, name, out);
      }
      return out;
    }
    getElementsByClassName(className) {
      const out = new HTMLCollection();
      if (this.documentElement) {
        if (this.documentElement.classList.contains(String(className))) out.push(this.documentElement);
        collectByClass(this.documentElement, String(className), out);
      }
      return out;
    }
    getElementsByName(name) {
      const matches2 = (e) => e.getAttributeInternal("name") === name;
      const out = new HTMLCollection();
      if (this.documentElement) {
        if (matches2(this.documentElement)) out.push(this.documentElement);
        collectByPredicate(this.documentElement, matches2, out);
      }
      return out;
    }
    get scripts() {
      return this.getElementsByTagName("script");
    }
    // The foreground answers: the tab is visible, it has focus, and nothing is focused past the body. Bot
    // management and session recorders read these during init and dereference what they get, so a missing one
    // throws instead of taking the backgrounded branch it was written for.
    hasFocus() {
      return true;
    }
    get activeElement() {
      return this.body || this.documentElement;
    }
    // No layout, so nothing truly occupies a point. A recorder hit-testing its own cursor trail gets the
    // element a browser would always have under one, and null outside the viewport — the answer it guards
    // for already, since a browser returns null there too.
    elementFromPoint(x, y) {
      return withinViewport(x, y) ? this.body || this.documentElement : null;
    }
    elementsFromPoint(x, y) {
      if (!withinViewport(x, y)) return [];
      return [this.body, this.documentElement].filter((e) => e !== null);
    }
    get fonts() {
      return this._fonts || (this._fonts = createFontFaceSet());
    }
    querySelector(sel) {
      const r = querySelectorAll(this, sel);
      return r.length ? r[0] : null;
    }
    querySelectorAll(sel) {
      return querySelectorAll(this, sel);
    }
    // The pre-constructor construction path: create it untyped, then name it through initEvent /
    // initCustomEvent. An analytics shim builds every event this way and reads nothing back, so what matters
    // is that the object it gets is a real Event carrying the initializer the family it asked for defines.
    createEvent(kind) {
      const family = String(kind || "Event").toLowerCase();
      return family.startsWith("custom") ? new CustomEvent("") : new Event("");
    }
    // jQuery's UMD factory feature-detects against `implementation.createHTMLDocument` during init; a missing
    // implementation threw before the global was assigned, so later bundles saw "jQuery is not defined".
    get implementation() {
      return {
        hasFeature: () => true,
        createDocumentType: (name, publicId, systemId) => new DocumentType(name, publicId ?? "", systemId ?? ""),
        // The XML sibling of createHTMLDocument, reached the same way: an SVG or feed helper calls it
        // during init with no feature test, so its absence costs that helper's whole script. The document
        // it answers with is an ordinary one carrying the named root element — namespaces are not modelled.
        createDocument: (_ns, qualifiedName, doctype) => {
          const d = new _Document();
          if (doctype) d.appendChild(doctype);
          if (qualifiedName) {
            const root = d.createElement(String(qualifiedName));
            d.appendChild(root);
            d.documentElement = root;
          }
          return d;
        },
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

  // dom/CDATASection.ts
  var CDATASection = class _CDATASection extends CharacterData {
    constructor(data) {
      super(4 /* CdataSection */, data);
      hideOwnFields(this);
    }
    get nodeName() {
      return "#cdata-section";
    }
    _shallowClone() {
      return new _CDATASection(this.data);
    }
  };

  // dom/ProcessingInstruction.ts
  var ProcessingInstruction = class _ProcessingInstruction extends CharacterData {
    constructor(target, data) {
      super(7 /* ProcessingInstruction */, data);
      this.target = String(target ?? "");
      hideOwnFields(this);
    }
    get nodeName() {
      return this.target;
    }
    _shallowClone() {
      return new _ProcessingInstruction(this.target, this.data);
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
  var HTMLButtonElement = class extends HTMLElement {
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
    // The fallback arm of the quirksmode-descended sniffer chat and consent widgets still ship:
    // `searchVersion(navigator.userAgent) || searchVersion(navigator.appVersion)`, where each arm indexes the
    // string it is handed. The second always runs here — userAgent carries no version for the first to find —
    // so undefined is `undefined.indexOf(…)`, a TypeError aborting the whole chunk. Same string as userAgent:
    // a sniffer matching on one and not the other is reading two different browsers.
    appVersion: "SimpleCrawler",
    platform: "",
    // Read bare and immediately indexed (`for (i = 0, n = navigator.plugins.length; …)`) by those same
    // detectors, so absence is a TypeError where an empty list is a path they handle — and empty is the
    // truthful answer, this render loads no plugins.
    plugins: [],
    language: "en",
    geolocation: {
      getCurrentPosition() {
      },
      watchPosition() {
        return 0;
      },
      clearWatch() {
      }
    },
    // The beacon an analytics bundle sends on its way out. Reporting success is the point: this render
    // installs no fetch/XHR by default precisely so such a bundle runs and sets its globals while its beacon
    // goes nowhere, and sendBeacon was the one exit that threw instead of quietly no-opping. Returning false
    // would invite the documented fallback — re-send over XHR — which is the path we are avoiding.
    sendBeacon() {
      return true;
    }
    // declined: connection — and the measurement is the reason, not an oversight. Unlike sendBeacon above,
    // every observed read is *guarded* (`navigator.connection && …`), so its absence is already a path real
    // pages take deliberately; supplying a stub instead diverts them onto their adaptive branch on the
    // strength of a connection we invented, and it recovered no global on any sampled target. A shim whose
    // only measured effect is to change which branch a page takes is surface this cannot justify.
    // declined: serviceWorker — same shape. Feature-detected via `"serviceWorker" in navigator`, so absence
    // is the clean, handled path, and no sampled target read it at all. Revisit if a target is shown losing a
    // global to either.
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

  // browser/UIEvents.ts
  var UIEvent = class extends Event {
    constructor(type, init) {
      super(type, init);
      this.detail = init && init.detail ? Number(init.detail) : 0;
      this.view = init && init.view !== void 0 ? init.view : null;
    }
  };
  var MouseEvent = class extends UIEvent {
    constructor(type, init) {
      super(type, init);
      const i = init || {};
      this.button = Number(i.button) || 0;
      this.buttons = Number(i.buttons) || 0;
      this.clientX = Number(i.clientX) || 0;
      this.clientY = Number(i.clientY) || 0;
      this.screenX = Number(i.screenX) || 0;
      this.screenY = Number(i.screenY) || 0;
      this.pageX = Number(i.pageX) || this.clientX;
      this.pageY = Number(i.pageY) || this.clientY;
      this.altKey = !!i.altKey;
      this.ctrlKey = !!i.ctrlKey;
      this.metaKey = !!i.metaKey;
      this.shiftKey = !!i.shiftKey;
      this.relatedTarget = i.relatedTarget !== void 0 ? i.relatedTarget : null;
    }
  };
  var PointerEvent = class extends MouseEvent {
    constructor(type, init) {
      super(type, init);
      const i = init || {};
      this.pointerId = Number(i.pointerId) || 0;
      this.pointerType = i.pointerType ? String(i.pointerType) : "";
      this.isPrimary = !!i.isPrimary;
    }
  };
  var KeyboardEvent = class extends UIEvent {
    constructor(type, init) {
      super(type, init);
      const i = init || {};
      this.key = i.key ? String(i.key) : "";
      this.code = i.code ? String(i.code) : "";
      this.keyCode = Number(i.keyCode) || 0;
      this.which = Number(i.which) || this.keyCode;
      this.repeat = !!i.repeat;
      this.altKey = !!i.altKey;
      this.ctrlKey = !!i.ctrlKey;
      this.metaKey = !!i.metaKey;
      this.shiftKey = !!i.shiftKey;
    }
  };
  var FocusEvent = class extends UIEvent {
    constructor(type, init) {
      super(type, init);
      this.relatedTarget = init && init.relatedTarget !== void 0 ? init.relatedTarget : null;
    }
  };
  var InputEvent = class extends UIEvent {
    constructor(type, init) {
      super(type, init);
      const i = init || {};
      this.data = i.data !== void 0 ? String(i.data) : null;
      this.inputType = i.inputType ? String(i.inputType) : "";
    }
  };
  var WheelEvent = class extends MouseEvent {
    constructor(type, init) {
      super(type, init);
      const i = init || {};
      this.deltaX = Number(i.deltaX) || 0;
      this.deltaY = Number(i.deltaY) || 0;
      this.deltaMode = Number(i.deltaMode) || 0;
    }
  };

  // browser/PromiseRejectionEvent.ts
  var PromiseRejectionEvent = class extends Event {
    constructor(type, init) {
      super(type, init);
      this.promise = init ? init.promise : void 0;
      this.reason = init ? init.reason : void 0;
    }
  };

  // browser/DOMRect.ts
  var DOMRectReadOnly = class _DOMRectReadOnly {
    constructor(x, y, width, height) {
      this.x = +x || 0;
      this.y = +y || 0;
      this.width = +width || 0;
      this.height = +height || 0;
    }
    get top() {
      return Math.min(this.y, this.y + this.height);
    }
    get bottom() {
      return Math.max(this.y, this.y + this.height);
    }
    get left() {
      return Math.min(this.x, this.x + this.width);
    }
    get right() {
      return Math.max(this.x, this.x + this.width);
    }
    toJSON() {
      return {
        x: this.x,
        y: this.y,
        width: this.width,
        height: this.height,
        top: this.top,
        right: this.right,
        bottom: this.bottom,
        left: this.left
      };
    }
    static fromRect(other) {
      other = other || {};
      return new _DOMRectReadOnly(other.x, other.y, other.width, other.height);
    }
  };
  var DOMRect = class _DOMRect extends DOMRectReadOnly {
    static fromRect(other) {
      other = other || {};
      return new _DOMRect(other.x, other.y, other.width, other.height);
    }
  };

  // dom/OffscreenCanvas.ts
  var OffscreenCanvas = class {
    constructor(width, height) {
      this.width = width || 0;
      this.height = height || 0;
    }
    getContext(type) {
      return type === "2d" ? createContext2D(this) : null;
    }
    transferToImageBitmap() {
      return { width: this.width, height: this.height, close() {
      } };
    }
    convertToBlob() {
      return Promise.resolve(null);
    }
    close() {
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

  // network/types/AbortSignal.ts
  var AbortSignal = class {
    constructor() {
      this.aborted = false;
    }
    throwIfAborted() {
    }
    addEventListener(type = null, listener = null, options = null) {
    }
    removeEventListener(type = null, listener = null, options = null) {
    }
    dispatchEvent(evt = null) {
      return true;
    }
  };

  // network/types/AbortController.ts
  var AbortController = class {
    constructor() {
      this.signal = new AbortSignal();
    }
    abort(reason) {
    }
  };

  // network/XMLHttpRequestEventTarget.ts
  var XMLHttpRequestEventTarget = class {
    constructor() {
      this._listeners = {};
    }
    addEventListener(type, cb) {
      if (typeof cb !== "function") return;
      (this._listeners[type] || (this._listeners[type] = [])).push(cb);
    }
    removeEventListener(type, cb) {
      const list = this._listeners[type];
      if (!list) return;
      const index = list.indexOf(cb);
      if (index >= 0) list.splice(index, 1);
    }
    dispatchEvent(event) {
      const list = event && this._listeners[event.type];
      if (list) {
        for (const cb of list.slice()) {
          try {
            cb.call(this, event);
          } catch {
          }
        }
      }
      return true;
    }
  };

  // network/XMLHttpRequestStub.ts
  var XMLHttpRequestStub = class extends XMLHttpRequestEventTarget {
    constructor() {
      super();
      this.readyState = 0;
      this.status = 0;
      this.statusText = "";
      this.responseText = "";
      this.response = "";
      this.responseType = "";
      this.responseURL = "";
      this.withCredentials = false;
      this.timeout = 0;
      this.onreadystatechange = null;
      this.onload = null;
      this.onerror = null;
      this.onloadend = null;
      this.onloadstart = null;
      this.onprogress = null;
      this.onabort = null;
      this.ontimeout = null;
      this.upload = new XMLHttpRequestEventTarget();
    }
    open() {
      this.readyState = 1;
    }
    setRequestHeader() {
    }
    send() {
    }
    abort() {
    }
    overrideMimeType() {
    }
    getResponseHeader() {
      return null;
    }
    getAllResponseHeaders() {
      return "";
    }
  };
  XMLHttpRequestStub.UNSENT = 0;
  XMLHttpRequestStub.OPENED = 1;
  XMLHttpRequestStub.HEADERS_RECEIVED = 2;
  XMLHttpRequestStub.LOADING = 3;
  XMLHttpRequestStub.DONE = 4;

  // network/fetchStub.ts
  function fetchStub() {
    return Promise.reject(new TypeError("Failed to fetch"));
  }

  // browser/BroadcastChannel.ts
  var _channels = {};
  var BroadcastChannel = class {
    constructor(name) {
      this.onmessage = null;
      this.onmessageerror = null;
      this.closed = false;
      this.name = String(name);
      (_channels[this.name] || (_channels[this.name] = [])).push(this);
    }
    postMessage(data) {
      if (this.closed) return;
      for (const peer of _channels[this.name] || []) {
        if (peer === this || peer.closed) continue;
        enqueue(() => {
          if (peer.onmessage) peer.onmessage({ data, type: "message", target: peer });
        });
      }
    }
    close() {
      this.closed = true;
      const peers = _channels[this.name];
      if (!peers) return;
      const at = peers.indexOf(this);
      if (at >= 0) peers.splice(at, 1);
    }
    addEventListener(type, cb) {
      if (type === "message") this.onmessage = cb;
      else if (type === "messageerror") this.onmessageerror = cb;
    }
    removeEventListener(type, cb) {
      if (type === "message" && this.onmessage === cb) this.onmessage = null;
      else if (type === "messageerror" && this.onmessageerror === cb) this.onmessageerror = null;
    }
    dispatchEvent() {
      return true;
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
  var _epochStart = Date.now();
  var PerformanceTiming = class {
    constructor() {
      this.navigationStart = _epochStart;
      this.unloadEventStart = 0;
      this.unloadEventEnd = 0;
      this.redirectStart = 0;
      this.redirectEnd = 0;
      this.fetchStart = _epochStart;
      this.domainLookupStart = _epochStart;
      this.domainLookupEnd = _epochStart;
      this.connectStart = _epochStart;
      this.connectEnd = _epochStart;
      this.secureConnectionStart = 0;
      this.requestStart = _epochStart;
      this.responseStart = _epochStart;
      this.responseEnd = _epochStart;
      this.domLoading = _epochStart;
      this.domInteractive = _epochStart;
      this.domContentLoadedEventStart = _epochStart;
      this.domContentLoadedEventEnd = _epochStart;
      this.domComplete = _epochStart;
      this.loadEventStart = _epochStart;
      this.loadEventEnd = _epochStart;
    }
  };
  var PerformanceNavigationTiming = class {
    constructor() {
      this.entryType = "navigation";
      // Assigned when the entry is handed out, not here: this instance is built while the prelude loads, and
      // the page URL only arrives afterwards (__crawlerSetLocation).
      this.name = "";
      this.initiatorType = "navigation";
      this.type = "navigate";
      this.startTime = 0;
      this.duration = 0;
      this.fetchStart = 0;
      this.domainLookupStart = 0;
      this.domainLookupEnd = 0;
      this.connectStart = 0;
      this.connectEnd = 0;
      this.secureConnectionStart = 0;
      this.requestStart = 0;
      this.responseStart = 0;
      this.responseEnd = 0;
      this.domInteractive = 0;
      this.domContentLoadedEventStart = 0;
      this.domContentLoadedEventEnd = 0;
      this.domComplete = 0;
      this.loadEventStart = 0;
      this.loadEventEnd = 0;
      this.redirectCount = 0;
      this.transferSize = 0;
      this.encodedBodySize = 0;
      this.decodedBodySize = 0;
    }
    toJSON() {
      return { ...this };
    }
  };
  var Performance = class {
    constructor() {
      this.timeOrigin = startTime;
      this.timing = new PerformanceTiming();
      // The Level 1 navigation type: 0 is TYPE_NAVIGATE, and no redirect was followed inside the render.
      this.navigation = { type: 0, redirectCount: 0 };
      this._navigationEntry = new PerformanceNavigationTiming();
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
    // Only the navigation entry exists: no resource, paint or long-task entry is observable in a render that
    // fetches through the host and never paints. Every other type answers with the empty list a browser gives
    // before anything of that type has happened.
    getEntries() {
      return [this._entry()];
    }
    getEntriesByName(name) {
      const entry = this._entry();
      return entry.name === String(name) ? [entry] : [];
    }
    getEntriesByType(type) {
      return String(type) === "navigation" ? [this._entry()] : [];
    }
    _entry() {
      this._navigationEntry.name = String(globalThis.location?.href || "");
      return this._navigationEntry;
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

  // browser/PerformanceObserver.ts
  var _supportedEntryTypes = [
    "element",
    "event",
    "first-input",
    "largest-contentful-paint",
    "layout-shift",
    "longtask",
    "mark",
    "measure",
    "navigation",
    "paint",
    "resource",
    "visibility-state"
  ];
  var PerformanceObserver = class {
    constructor(_callback) {
    }
    observe() {
    }
    disconnect() {
    }
    takeRecords() {
      return [];
    }
  };
  PerformanceObserver.supportedEntryTypes = _supportedEntryTypes;

  // browser/Worker.ts
  var Worker = class {
    constructor(_url, _options) {
      this.onmessage = null;
      this.onmessageerror = null;
      this.onerror = null;
    }
    postMessage() {
    }
    terminate() {
    }
    addEventListener() {
    }
    removeEventListener() {
    }
    dispatchEvent() {
      return false;
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

  // browser/File.ts
  var File = class extends Blob {
    constructor(parts, name, options) {
      super(parts, options);
      this.webkitRelativePath = "";
      this.name = name == null ? "" : String(name);
      this.lastModified = options && options.lastModified != null ? Number(options.lastModified) : Date.now();
    }
  };

  // browser/Window.ts
  var Window = class {
    constructor() {
      throw new TypeError("Illegal constructor");
    }
  };
  Object.defineProperty(Window, Symbol.hasInstance, {
    value: (value) => value === globalThis
  });

  // network/types/FormData.ts
  var FormData = class {
    constructor() {
      this._e = [];
    }
    append(name, value) {
      this._e.push([String(name), value]);
    }
    delete(name) {
      const n = String(name);
      this._e = this._e.filter((p) => p[0] !== n);
    }
    get(name) {
      const n = String(name);
      for (const p of this._e) if (p[0] === n) return p[1];
      return null;
    }
    getAll(name) {
      const n = String(name);
      const out = [];
      for (const p of this._e) if (p[0] === n) out.push(p[1]);
      return out;
    }
    has(name) {
      const n = String(name);
      for (const p of this._e) if (p[0] === n) return true;
      return false;
    }
    set(name, value) {
      const n = String(name);
      let added = false;
      const out = [];
      for (const p of this._e) {
        if (p[0] === n) {
          if (!added) {
            out.push([n, value]);
            added = true;
          }
        } else out.push(p);
      }
      if (!added) out.push([n, value]);
      this._e = out;
    }
    entries() {
      let i = 0;
      const d = this._e;
      return { next() {
        return i < d.length ? { value: d[i++], done: false } : { value: void 0, done: true };
      } };
    }
    keys() {
      let i = 0;
      const d = this._e;
      return { next() {
        return i < d.length ? { value: d[i++][0], done: false } : { value: void 0, done: true };
      } };
    }
    values() {
      let i = 0;
      const d = this._e;
      return { next() {
        return i < d.length ? { value: d[i++][1], done: false } : { value: void 0, done: true };
      } };
    }
    forEach(cb, thisArg) {
      for (const p of this._e) cb.call(thisArg, p[1], p[0], this);
    }
  };
  FormData.prototype[Symbol.iterator] = FormData.prototype.entries;

  // network/utils.ts
  function toHeaderObject(h) {
    const out = {};
    if (!h) return out;
    if (typeof h.forEach === "function" && !Array.isArray(h)) {
      h.forEach((v, k) => {
        out[k] = v;
      });
      return out;
    }
    if (Array.isArray(h)) {
      for (let i = 0; i < h.length; i++) {
        out[h[i][0]] = h[i][1];
      }
      return out;
    }
    for (const k in h) {
      if (Object.prototype.hasOwnProperty.call(h, k)) out[k] = h[k];
    }
    return out;
  }

  // network/types/Headers.ts
  var Headers = class {
    constructor(init) {
      this._m = {};
      const o = toHeaderObject(init);
      for (const k in o) {
        this._m[String(k).toLowerCase()] = String(o[k]);
      }
    }
    get(n) {
      const v = this._m[String(n).toLowerCase()];
      return v === void 0 ? null : v;
    }
    has(n) {
      return this._m[String(n).toLowerCase()] !== void 0;
    }
    set(n, v) {
      this._m[String(n).toLowerCase()] = String(v);
    }
    append(n, v) {
      const k = String(n).toLowerCase();
      this._m[k] = this._m[k] !== void 0 ? this._m[k] + ", " + v : String(v);
    }
    delete(n) {
      delete this._m[String(n).toLowerCase()];
    }
    forEach(cb) {
      for (const k in this._m) {
        cb(this._m[k], k, this);
      }
    }
    keys() {
      return Object.keys(this._m);
    }
  };

  // network/types/Request.ts
  var Request = class {
    constructor(input, init) {
      init = init || {};
      if (input && typeof input === "object" && "url" in input) {
        this.url = input.url;
        this.method = init.method || input.method || "GET";
        this.headers = new Headers(init.headers || input.headers);
        this.body = init.body !== void 0 ? init.body : input.body;
      } else {
        this.url = String(input);
        this.method = init.method || "GET";
        this.headers = new Headers(init.headers);
        this.body = init.body;
      }
    }
  };

  // network/types/Response.ts
  var Response = class _Response {
    constructor(body, init) {
      init = init || {};
      this._bodyText = body == null ? "" : String(body);
      this._bodyStream = void 0;
      this.status = init.status === void 0 ? 200 : init.status;
      this.ok = this.status >= 200 && this.status < 300;
      this.statusText = init.statusText || "";
      this.url = "";
      this.redirected = false;
      this.type = "default";
      this.headers = init.headers instanceof Headers ? init.headers : new Headers(init.headers);
      this.bodyUsed = false;
    }
    // Exposes the buffered body as a ReadableStream only when the Streams shim (EnableStreams) is
    // installed; otherwise null, as in a browser without a stream body. Cached so repeat access returns
    // the same stream (spec) and reflects bodyUsed once read.
    get body() {
      const g = globalThis;
      if (typeof g.ReadableStream !== "function") return null;
      if (this._bodyStream === void 0) {
        const bytes = new TextEncoder().encode(this._bodyText);
        this._bodyStream = new g.ReadableStream({
          start: (controller) => {
            if (bytes.length) controller.enqueue(bytes);
            controller.close();
            this.bodyUsed = true;
          }
        });
      }
      return this._bodyStream;
    }
    text() {
      this.bodyUsed = true;
      return Promise.resolve(this._bodyText);
    }
    json() {
      try {
        return Promise.resolve(JSON.parse(this._bodyText || "null"));
      } catch (e) {
        return Promise.reject(e);
      }
    }
    arrayBuffer() {
      this.bodyUsed = true;
      return Promise.resolve(new TextEncoder().encode(this._bodyText).buffer);
    }
    clone() {
      const c = new _Response(this._bodyText, { status: this.status, statusText: this.statusText, headers: this.headers });
      c.ok = this.ok;
      c.url = this.url;
      c.type = this.type;
      c.redirected = this.redirected;
      return c;
    }
  };

  // browser/DOMParser.ts
  var DOMParser = class {
    parseFromString(input, type) {
      const mime = String(type ?? "").toLowerCase();
      const doc2 = new Document();
      if (mime.indexOf("xml") >= 0 || mime.indexOf("svg") >= 0) {
        const root = parseFragment(input).find((n) => n.nodeType === 1 /* Element */) || null;
        if (root) {
          root.parentNode = doc2;
          doc2.documentElement = root;
          doc2.childNodes = [root];
        }
        return doc2;
      }
      parseHTML(doc2, input);
      return doc2;
    }
  };

  // browser/XMLSerializer.ts
  var XMLSerializer = class {
    serializeToString(node) {
      if (node == null || typeof node.nodeType !== "number") return "";
      return serializeNode(node);
    }
  };

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

  // css/CSS.ts
  function escape(value) {
    const input = String(value);
    const out = [];
    const first = input.charCodeAt(0);
    for (let i = 0; i < input.length; i++) {
      const code = input.charCodeAt(i);
      if (code === 0) {
        out.push("\uFFFD");
        continue;
      }
      if (code >= 1 && code <= 31 || code === 127 || i === 0 && code >= 48 && code <= 57 || i === 1 && code >= 48 && code <= 57 && first === 45) {
        out.push("\\" + code.toString(16) + " ");
        continue;
      }
      if (i === 0 && code === 45 && input.length === 1) {
        out.push("\\" + input.charAt(i));
        continue;
      }
      if (code >= 128 || code === 45 || code === 95 || code >= 48 && code <= 57 || code >= 65 && code <= 90 || code >= 97 && code <= 122) {
        out.push(input.charAt(i));
        continue;
      }
      out.push("\\" + input.charAt(i));
    }
    return out.join("");
  }
  function supports(conditionOrProperty, value) {
    if (value === void 0) return String(conditionOrProperty).trim().length > 0;
    return String(conditionOrProperty).trim().length > 0 && String(value).trim().length > 0;
  }
  var CSS = {
    escape,
    supports,
    // Houdini's custom-property registration. A page registers at init and reads nothing back, so recording
    // nothing is enough for init to survive; the render has no cascade for a registration to reach.
    registerProperty() {
    }
    // declined: CSS.px and the rest of the numeric factories (Typed OM), CSS.highlights. Neither was observed
    // on a target, and both return live objects whose arithmetic a caller would then trust.
  };

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
    global.frames = global;
    global.top = global;
    global.parent = global;
    if (!("length" in global)) global.length = 0;
    if (typeof global.name !== "string") global.name = "";
    Object.defineProperty(global, Symbol.toStringTag, { value: "Window", configurable: true });
    global.navigator = navigator;
    global.location = createLocation();
    global.history = createHistory();
    global.addEventListener = (t, cb) => addListener(_windowListeners, t, cb);
    global.removeEventListener = (t, cb) => removeListener(_windowListeners, t, cb);
    global.dispatchEvent = (event) => fireEvent(global, _windowListeners, event);
    for (const method of ["addEventListener", "removeEventListener", "dispatchEvent"]) {
      Window.prototype[method] = global[method];
    }
    try {
      Object.setPrototypeOf(Window.prototype, EventTarget.prototype);
      Object.setPrototypeOf(global, Window.prototype);
    } catch {
    }
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
    global.CSS = global.CSS || CSS;
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
    global.PerformanceObserver = global.PerformanceObserver || PerformanceObserver;
    global.Worker = global.Worker || Worker;
    global.structuredClone = global.structuredClone || ((value) => value == null ? value : JSON.parse(JSON.stringify(value)));
    global.Blob = Blob;
    global.File = global.File || File;
    global.FormData = global.FormData || FormData;
    global.Headers = global.Headers || Headers;
    global.Request = global.Request || Request;
    global.Response = global.Response || Response;
    global.DOMException = global.DOMException || DOMException;
    global.DOMParser = global.DOMParser || DOMParser;
    global.XMLSerializer = global.XMLSerializer || XMLSerializer;
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
    global.HTMLCollection = HTMLCollection;
    global.Element = Element;
    global.CharacterData = CharacterData;
    global.DOMTokenList = DOMTokenList;
    global.NodeFilter = NodeFilter;
    global.TreeWalker = TreeWalker;
    global.NodeIterator = NodeIterator;
    global.Window = global.Window || Window;
    global.Document = Document;
    global.DocumentType = DocumentType;
    global.Text = Text;
    global.Comment = Comment;
    global.CDATASection = CDATASection;
    global.ProcessingInstruction = ProcessingInstruction;
    global.DocumentFragment = DocumentFragment;
    global.HTMLElement = HTMLElement;
    global.HTMLTemplateElement = HTMLTemplateElement;
    global.CSSTransition = CSSTransition;
    global.Image = HTMLImageElement;
    for (const name in htmlInterfaces_exports) global[name] = htmlInterfaces_exports[name];
    global.customElements = customElements;
    global.CustomElementRegistry = CustomElementRegistry;
    global.ShadowRoot = ShadowRoot;
    customElements.setDocument(doc);
    global.Event = Event;
    global.CustomEvent = CustomEvent;
    global.UIEvent = UIEvent;
    global.MouseEvent = MouseEvent;
    global.PointerEvent = PointerEvent;
    global.KeyboardEvent = KeyboardEvent;
    global.FocusEvent = FocusEvent;
    global.InputEvent = InputEvent;
    global.WheelEvent = WheelEvent;
    global.PromiseRejectionEvent = global.PromiseRejectionEvent || PromiseRejectionEvent;
    global.DOMRect = global.DOMRect || DOMRect;
    global.DOMRectReadOnly = global.DOMRectReadOnly || DOMRectReadOnly;
    global.OffscreenCanvas = global.OffscreenCanvas || OffscreenCanvas;
    global.TextEncoder = global.TextEncoder || TextEncoder;
    global.TextDecoder = global.TextDecoder || TextDecoder;
    global.crypto = global.crypto || crypto;
    global.AbortController = global.AbortController || AbortController;
    global.AbortSignal = global.AbortSignal || AbortSignal;
    global.XMLHttpRequestEventTarget = global.XMLHttpRequestEventTarget || XMLHttpRequestEventTarget;
    global.XMLHttpRequest = global.XMLHttpRequest || XMLHttpRequestStub;
    global.fetch = global.fetch || fetchStub;
    global.MessageChannel = global.MessageChannel || MessageChannel;
    global.BroadcastChannel = global.BroadcastChannel || BroadcastChannel;
    global.MessagePort = global.MessagePort || MessagePort;
    global.postMessage = global.postMessage || ((message, targetOrigin, transfer) => {
      const ports = Array.isArray(targetOrigin) ? targetOrigin : Array.isArray(transfer) ? transfer : [];
      enqueue(() => {
        const event = { type: "message", data: message, origin: "", lastEventId: "", source: global, ports };
        if (typeof global.onmessage === "function") {
          try {
            global.onmessage(event);
          } catch {
          }
        }
        global.dispatchEvent(event);
      });
    });
    global.performance = global.performance || performance;
    global.Storage = global.Storage || Storage;
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
  var _scriptNodes = [];
  function setCurrentScript(script) {
    if (script == null) {
      doc.currentScript = null;
      return;
    }
    if (typeof script === "number") {
      doc.currentScript = _scriptNodes[script] || null;
      return;
    }
    const node = new HTMLScriptElement();
    const s = String(script);
    if (s) node.src = s;
    node.parentNode = doc.head || doc.body || doc.documentElement;
    doc.currentScript = node;
  }
  function collectScripts() {
    const out = [];
    _scriptNodes = [];
    if (!doc.documentElement) return out;
    function walk(n) {
      for (const c of n.childNodes) {
        if (c.nodeType !== 1 /* Element */) continue;
        if (c.localName === "script") {
          const type = c.getAttributeInternal("type") || "";
          if (type && type !== "text/javascript" && type !== "module" && type !== "application/javascript") {
            walk(c);
            continue;
          }
          if (c.hasAttribute("nomodule")) {
            walk(c);
            continue;
          }
          const external = !!c.getAttributeInternal("src");
          out.push({
            module: type === "module",
            external,
            src: c.getAttributeInternal("src") || "",
            text: c.textContent,
            deferred: external && (c.hasAttribute("async") || c.hasAttribute("defer")),
            index: _scriptNodes.push(c) - 1
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
          const href = c.getAttributeInternal("href");
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
    let canonical2 = null;
    let robots = null;
    if (!doc.documentElement) return { anchors, canonical: canonical2, robots };
    function walk(n) {
      for (const c of n.childNodes) {
        if (c.nodeType !== 1 /* Element */) continue;
        const tag = c.localName;
        if (tag === "a") {
          anchors.push(c.getAttributeInternal("href"));
        } else if (canonical2 == null && tag === "link") {
          const rel = (c.getAttributeInternal("rel") || "").toLowerCase().split(/\s+/);
          if (rel.indexOf("canonical") >= 0) canonical2 = c.getAttributeInternal("href");
        } else if (robots == null && tag === "meta") {
          if ((c.getAttributeInternal("name") || "").toLowerCase() === "robots") robots = c.getAttributeInternal("content");
        }
        walk(c);
      }
    }
    walk(doc.documentElement);
    return { anchors, canonical: canonical2, robots };
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
