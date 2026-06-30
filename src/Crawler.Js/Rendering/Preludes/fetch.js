"use strict";
(() => {
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

  // network/types/Response.ts
  var Response = class _Response {
    constructor(r) {
      this._r = r;
      this.ok = !!r.ok;
      this.status = r.status;
      this.statusText = r.statusText || "";
      this.url = r.url || "";
      this.redirected = false;
      this.type = "basic";
      let parsed = {};
      try {
        parsed = JSON.parse(r.headersJson || "{}");
      } catch (e) {
      }
      this.headers = new Headers(parsed);
      this.bodyUsed = false;
    }
    text() {
      return Promise.resolve(this._r.body || "");
    }
    json() {
      try {
        return Promise.resolve(JSON.parse(this._r.body || "null"));
      } catch (e) {
        return Promise.reject(e);
      }
    }
    clone() {
      return new _Response(this._r);
    }
  };

  // network/fetch.ts
  function fetch(input, init) {
    init = init || {};
    if (input && typeof input === "object" && typeof input.href === "string" && typeof input.url !== "string") input = input.href;
    let url, method, headers, body;
    if (input && typeof input === "object" && "url" in input) {
      url = input.url;
      method = init.method || input.method || "GET";
      headers = init.headers || input.headers;
      body = init.body !== void 0 ? init.body : input.body;
    } else {
      url = String(input);
      method = init.method || "GET";
      headers = init.headers;
      body = init.body;
    }
    const r = __http.request(url, method, JSON.stringify(toHeaderObject(headers)), body == null ? null : String(body));
    if (r.error) return Promise.reject(new TypeError(r.error));
    return Promise.resolve(new Response(r));
  }

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

  // network/XMLHttpRequest.ts
  var XMLHttpRequest = class {
    constructor() {
      this.readyState = 0;
      this.status = 0;
      this.statusText = "";
      this.responseText = "";
      this.response = "";
      this._h = {};
      this._rh = "{}";
      this._method = "GET";
      this._url = "";
      this.onreadystatechange = null;
      this.onload = null;
      this.onerror = null;
      this.onloadend = null;
    }
    open(m, u) {
      this._method = m;
      this._url = u;
      this.readyState = 1;
      if (this.onreadystatechange) this.onreadystatechange();
    }
    setRequestHeader(k, v) {
      this._h[k] = v;
    }
    send(body) {
      const r = __http.request(this._url, this._method, JSON.stringify(this._h), body == null ? null : String(body));
      if (r.error) {
        this.status = 0;
        this.readyState = 4;
        if (this.onerror) this.onerror(new Error(r.error));
        if (this.onloadend) this.onloadend();
        return;
      }
      this.status = r.status;
      this.statusText = r.statusText || "";
      this.responseText = r.body;
      this.response = r.body;
      this._rh = r.headersJson || "{}";
      this.readyState = 4;
      if (this.onreadystatechange) this.onreadystatechange();
      if (this.onload) this.onload();
      if (this.onloadend) this.onloadend();
    }
    abort() {
    }
    getResponseHeader(n) {
      try {
        const o = JSON.parse(this._rh);
        const v = o[n];
        return v === void 0 ? null : v;
      } catch (e) {
        return null;
      }
    }
    getAllResponseHeaders() {
      try {
        const o = JSON.parse(this._rh);
        let s = "";
        for (const k in o) {
          s += k + ": " + o[k] + "\r\n";
        }
        return s;
      } catch (e) {
        return "";
      }
    }
    addEventListener(t, cb) {
      if (t === "load") this.onload = cb;
      else if (t === "error") this.onerror = cb;
      else if (t === "loadend") this.onloadend = cb;
      else if (t === "readystatechange") this.onreadystatechange = cb;
    }
    removeEventListener() {
    }
  };
  XMLHttpRequest.UNSENT = 0;
  XMLHttpRequest.OPENED = 1;
  XMLHttpRequest.HEADERS_RECEIVED = 2;
  XMLHttpRequest.LOADING = 3;
  XMLHttpRequest.DONE = 4;

  // network/api.ts
  function installNetwork(global) {
    global.Headers = global.Headers || Headers;
    global.Response = global.Response || Response;
    global.Request = global.Request || Request;
    global.FormData = global.FormData || FormData;
    global.fetch = global.fetch || fetch;
    global.XMLHttpRequest = global.XMLHttpRequest || XMLHttpRequest;
    global.AbortController = global.AbortController || AbortController;
    global.AbortSignal = global.AbortSignal || AbortSignal;
  }

  // network/index.ts
  installNetwork(globalThis);
})();
