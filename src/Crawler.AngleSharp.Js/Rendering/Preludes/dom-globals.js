// Remaining DOM globals used only as instanceof right-hand sides; always false for a crawl.
// URLSearchParams gets a working body because routers parse query strings with it.
(function () {
  function def(name, ctor) { if (!globalThis[name]) globalThis[name] = ctor; }
  def('ShadowRoot', class ShadowRoot {});
  def('SVGElement', class SVGElement extends HTMLElement {});
  def('HTMLHtmlElement', class HTMLHtmlElement extends HTMLElement {});
  def('HTMLBodyElement', class HTMLBodyElement extends HTMLElement {});
  def('HTMLTextAreaElement', class HTMLTextAreaElement extends HTMLElement {});
  def('HTMLIFrameElement', class HTMLIFrameElement extends HTMLElement {});
  def('DOMException', class DOMException extends Error {});
  def('Blob', class Blob {});
  def('File', class File extends globalThis.Blob {});
  def('FileList', class FileList {});
  def('FormData', class FormData {
    append() {} delete() {} get() { return null; } getAll() { return []; }
    has() { return false; } set() {} forEach() {}
  });
  def('URLSearchParams', class URLSearchParams {
    constructor(init) {
      this._p = [];
      if (typeof init === 'string') {
        var s = init.charAt(0) === '?' ? init.slice(1) : init;
        var self = this;
        if (s) s.split('&').forEach(function (kv) {
          if (!kv) return;
          var i = kv.indexOf('=');
          var k = i < 0 ? kv : kv.slice(0, i);
          var v = i < 0 ? '' : kv.slice(i + 1);
          self._p.push([decodeURIComponent(k), decodeURIComponent(v.replace(/\+/g, ' '))]);
        });
      } else if (init && typeof init.forEach === 'function') {
        var s2 = this;
        init.forEach(function (v, k) { s2._p.push([k, String(v)]); });
      } else if (init) {
        for (var k in init) this._p.push([k, String(init[k])]);
      }
    }
    get(n) { for (var i = 0; i < this._p.length; i++) if (this._p[i][0] === n) return this._p[i][1]; return null; }
    getAll(n) { return this._p.filter(function (p) { return p[0] === n; }).map(function (p) { return p[1]; }); }
    has(n) { return this.get(n) !== null; }
    set(n, v) { this.delete(n); this._p.push([n, String(v)]); }
    append(n, v) { this._p.push([n, String(v)]); }
    delete(n) { this._p = this._p.filter(function (p) { return p[0] !== n; }); }
    forEach(cb) { var s = this; this._p.forEach(function (p) { cb(p[1], p[0], s); }); }
    keys() { return this._p.map(function (p) { return p[0]; }); }
    toString() { return this._p.map(function (p) { return encodeURIComponent(p[0]) + '=' + encodeURIComponent(p[1]); }).join('&'); }
  });
})();
