// Synchronous networking bridge over the host __http.request call. fetch returns an already-resolved
// Promise so .then()/await chains settle on the existing microtask drain without Task<->Promise bridging.
// Opt-in only: JsRenderOptions.EnableFetch, since it issues live HTTP requests.
(function () {
  function toHeaderObject(h) {
    var out = {};
    if (!h) return out;
    if (typeof h.forEach === 'function' && !Array.isArray(h)) { h.forEach(function (v, k) { out[k] = v; }); return out; }
    if (Array.isArray(h)) { for (var i = 0; i < h.length; i++) { out[h[i][0]] = h[i][1]; } return out; }
    for (var k in h) { if (Object.prototype.hasOwnProperty.call(h, k)) out[k] = h[k]; }
    return out;
  }
  class Headers {
    constructor(init) {
      this._m = {};
      var o = toHeaderObject(init);
      for (var k in o) { this._m[String(k).toLowerCase()] = String(o[k]); }
    }
    get(n) { var v = this._m[String(n).toLowerCase()]; return v === undefined ? null : v; }
    has(n) { return this._m[String(n).toLowerCase()] !== undefined; }
    set(n, v) { this._m[String(n).toLowerCase()] = String(v); }
    append(n, v) { var k = String(n).toLowerCase(); this._m[k] = this._m[k] !== undefined ? this._m[k] + ', ' + v : String(v); }
    delete(n) { delete this._m[String(n).toLowerCase()]; }
    forEach(cb) { for (var k in this._m) { cb(this._m[k], k, this); } }
    keys() { return Object.keys(this._m); }
  }
  class Response {
    constructor(r) {
      this._r = r; this.ok = !!r.ok; this.status = r.status; this.statusText = r.statusText || '';
      this.url = r.url || ''; this.redirected = false; this.type = 'basic';
      var parsed = {}; try { parsed = JSON.parse(r.headersJson || '{}'); } catch (e) {}
      this.headers = new Headers(parsed); this.bodyUsed = false;
    }
    text() { return Promise.resolve(this._r.body || ''); }
    json() { try { return Promise.resolve(JSON.parse(this._r.body || 'null')); } catch (e) { return Promise.reject(e); } }
    clone() { return new Response(this._r); }
  }
  class Request {
    constructor(input, init) {
      init = init || {};
      if (input && typeof input === 'object' && 'url' in input) {
        this.url = input.url; this.method = init.method || input.method || 'GET';
        this.headers = new Headers(init.headers || input.headers);
        this.body = init.body !== undefined ? init.body : input.body;
      } else {
        this.url = String(input); this.method = init.method || 'GET';
        this.headers = new Headers(init.headers); this.body = init.body;
      }
    }
  }
  function fetch(input, init) {
    init = init || {};
    // A URL host object stringifies to "[object Object]" under V8, so read its href explicitly.
    if (input && typeof input === 'object' && typeof input.href === 'string' && typeof input.url !== 'string') input = input.href;
    var url, method, headers, body;
    if (input && typeof input === 'object' && 'url' in input) {
      url = input.url; method = init.method || input.method || 'GET';
      headers = init.headers || input.headers; body = init.body !== undefined ? init.body : input.body;
    } else {
      url = String(input); method = init.method || 'GET'; headers = init.headers; body = init.body;
    }
    var r = __http.request(url, method, JSON.stringify(toHeaderObject(headers)), body == null ? null : String(body));
    if (r.error) return Promise.reject(new TypeError(r.error));
    return Promise.resolve(new Response(r));
  }
  class XMLHttpRequest {
    constructor() {
      this.readyState = 0; this.status = 0; this.statusText = ''; this.responseText = ''; this.response = '';
      this._h = {}; this._rh = '{}'; this._method = 'GET'; this._url = '';
      this.onreadystatechange = null; this.onload = null; this.onerror = null; this.onloadend = null;
    }
    open(m, u) { this._method = m; this._url = u; this.readyState = 1; if (this.onreadystatechange) this.onreadystatechange(); }
    setRequestHeader(k, v) { this._h[k] = v; }
    send(body) {
      var r = __http.request(this._url, this._method, JSON.stringify(this._h), body == null ? null : String(body));
      if (r.error) {
        this.status = 0; this.readyState = 4;
        if (this.onerror) this.onerror(new Error(r.error));
        if (this.onloadend) this.onloadend();
        return;
      }
      this.status = r.status; this.statusText = r.statusText || ''; this.responseText = r.body; this.response = r.body;
      this._rh = r.headersJson || '{}'; this.readyState = 4;
      if (this.onreadystatechange) this.onreadystatechange();
      if (this.onload) this.onload();
      if (this.onloadend) this.onloadend();
    }
    abort() {}
    getResponseHeader(n) { try { var o = JSON.parse(this._rh); var v = o[n]; return v === undefined ? null : v; } catch (e) { return null; } }
    getAllResponseHeaders() { try { var o = JSON.parse(this._rh); var s = ''; for (var k in o) { s += k + ': ' + o[k] + '\r\n'; } return s; } catch (e) { return ''; } }
    addEventListener(t, cb) {
      if (t === 'load') this.onload = cb;
      else if (t === 'error') this.onerror = cb;
      else if (t === 'loadend') this.onloadend = cb;
      else if (t === 'readystatechange') this.onreadystatechange = cb;
    }
    removeEventListener() {}
  }
  XMLHttpRequest.UNSENT = 0; XMLHttpRequest.OPENED = 1; XMLHttpRequest.HEADERS_RECEIVED = 2;
  XMLHttpRequest.LOADING = 3; XMLHttpRequest.DONE = 4;
  globalThis.Headers = globalThis.Headers || Headers;
  globalThis.Response = globalThis.Response || Response;
  globalThis.Request = globalThis.Request || Request;
  globalThis.fetch = globalThis.fetch || fetch;
  globalThis.XMLHttpRequest = globalThis.XMLHttpRequest || XMLHttpRequest;
})();
