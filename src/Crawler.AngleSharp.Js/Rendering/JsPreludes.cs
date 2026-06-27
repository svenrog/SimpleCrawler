namespace Crawler.AngleSharp.Js.Rendering;

internal static class JsPreludes
{
    // See the embedding site: CLR host types have no JS prototype, so the DOM/Web globals bundles use as an
    // instanceof right-hand side are JS shims whose Symbol.hasInstance defers to the host type check. URL and
    // Event also stay constructible by forwarding `new` to the privately embedded host type.
    public const string InstanceShims = """
        (function(){
          function ctor(host, kind){
            var f = function(){ return new host(...arguments); };
            Object.defineProperty(f, Symbol.hasInstance, {value:function(x){ return __isInstance(x, kind); }});
            return f;
          }
          function only(kind){
            var f = function(){};
            Object.defineProperty(f, Symbol.hasInstance, {value:function(x){ return __isInstance(x, kind); }});
            return f;
          }
          globalThis.URL = ctor(__ctor_URL, 'URL');
          globalThis.Event = ctor(__ctor_Event, 'Event');
          globalThis.CustomEvent = ctor(__ctor_CustomEvent, 'CustomEvent');
          var node = only('Node');
          node.ELEMENT_NODE=1; node.ATTRIBUTE_NODE=2; node.TEXT_NODE=3; node.CDATA_SECTION_NODE=4;
          node.PROCESSING_INSTRUCTION_NODE=7; node.COMMENT_NODE=8; node.DOCUMENT_NODE=9;
          node.DOCUMENT_TYPE_NODE=10; node.DOCUMENT_FRAGMENT_NODE=11;
          globalThis.Node = node;
          globalThis.Element = only('Element');
          globalThis.Text = only('Text');
          globalThis.Document = only('Document');
        })();
        """;

    // The bundle reaches the DOM through window/self; both are just the global object here.
    // structuredClone has no host equivalent, but the bundle only clones plain data, so a JSON
    // round-trip stands in (guarded so a native implementation, if present, wins).
    public const string Global =
        "var window=globalThis;var self=globalThis;" +
        "globalThis.structuredClone=globalThis.structuredClone||function(v){return v===undefined?undefined:JSON.parse(JSON.stringify(v));};";

    // crypto is a JS object (not the host wrapper directly) because uuid/nanoid bundles do
    // crypto.randomUUID.bind(crypto), and a V8/ClearScript host method is not a real JS function
    // (no .bind/.call). randomUUID delegates to the host for a real GUID; getRandomValues fills in JS.
    public const string Crypto =
        "globalThis.crypto=globalThis.crypto||{" +
        "randomUUID:function(){return __crypto.randomUUID();}," +
        "getRandomValues:function(a){if(a)for(var i=0;i<a.length;i++)a[i]=Math.floor(Math.random()*256);return a;}};";

    // The renderer fires load/error on a dynamically appended <script>/<link> by invoking the handler
    // the bundle stashed on the node (an expando) with a matching event object — see FireResourceEvent.
    public const string ResourceEvent =
        "globalThis.__invokeResourceEvent=function(h,t){if(typeof h==='function')h({type:t});};";

    // MessageChannel is how React's scheduler and state-batching helpers defer a flush ("if MessageChannel
    // is defined, port2.postMessage triggers port1.onmessage" — else a fallback that never runs here). Each
    // postMessage delivers to the paired port's onmessage as a macrotask via our setTimeout drain, so those
    // deferred flushes (which commit data-driven render updates) actually happen. instanceof type, stays JS.
    public const string MessageChannel = """
        (function(){
          if(globalThis.MessageChannel) return;
          function Port(){ this.onmessage=null; this._other=null; }
          Port.prototype.postMessage=function(data){
            var other=this._other;
            setTimeout(function(){ if(other&&other.onmessage) other.onmessage({data:data}); },0);
          };
          Port.prototype.start=function(){};
          Port.prototype.close=function(){};
          Port.prototype.addEventListener=function(t,cb){ if(t==='message') this.onmessage=cb; };
          Port.prototype.removeEventListener=function(t,cb){ if(t==='message'&&this.onmessage===cb) this.onmessage=null; };
          globalThis.MessagePort=globalThis.MessagePort||Port;
          globalThis.MessageChannel=class MessageChannel{
            constructor(){ this.port1=new Port(); this.port2=new Port(); this.port1._other=this.port2; this.port2._other=this.port1; }
          };
        })();
        """;

    // history is a plain JS object delegating to the host wrapper rather than the host object itself,
    // because routers (React Router's history lib) reassign history.pushState/replaceState and set
    // history.scrollRestoration — assignments a CLR host object rejects as read-only members.
    public const string History =
        "(function(){var h=__history;globalThis.history={" +
        "get length(){return h.length;},get state(){return h.state;},scrollRestoration:'auto'," +
        "pushState:function(s,t,u){return h.pushState(s,t,u);}," +
        "replaceState:function(s,t,u){return h.replaceState(s,t,u);}," +
        "go:function(d){return h.go(d);},back:function(){return h.back();},forward:function(){return h.forward();}};})();";

    // HTMLElement is the one DOM global that bundles *extend* (`class X extends HTMLElement`) rather
    // than construct, and V8/ClearScript can't `class extends` a CLR host type (its host objects have
    // no JS prototype) — so unlike Event/CustomEvent above it has to be a real JS class. It is never
    // instantiated (customElements.define is a no-op), so the body is just no-op stubs.
    public const string HtmlElement =
        "globalThis.HTMLElement=globalThis.HTMLElement||class HTMLElement{" +
        "addEventListener(){}removeEventListener(){}dispatchEvent(){return true;}attachShadow(){return this;}};" +
        "globalThis.HTMLScriptElement=globalThis.HTMLScriptElement||class HTMLScriptElement extends HTMLElement{};";

    // Remaining DOM/Web globals the bundle uses only as instanceof right-hand sides (no host wrapper
    // models them, so the check is always false — which is the correct answer for a crawl). They have
    // to exist as objects or instanceof throws. URLSearchParams is given a working body because the
    // router actually parses query strings with it. Per project convention instanceof types stay JS.
    public const string DomGlobals = """
        (function(){
          function def(name, ctor){ if(!globalThis[name]) globalThis[name]=ctor; }
          def('ShadowRoot', class ShadowRoot{});
          def('SVGElement', class SVGElement extends HTMLElement{});
          def('HTMLHtmlElement', class HTMLHtmlElement extends HTMLElement{});
          def('HTMLBodyElement', class HTMLBodyElement extends HTMLElement{});
          def('HTMLTextAreaElement', class HTMLTextAreaElement extends HTMLElement{});
          def('HTMLIFrameElement', class HTMLIFrameElement extends HTMLElement{});
          def('DOMException', class DOMException extends Error{});
          def('Blob', class Blob{});
          def('File', class File extends globalThis.Blob{});
          def('FileList', class FileList{});
          def('FormData', class FormData{ append(){} delete(){} get(){return null;} getAll(){return [];} has(){return false;} set(){} forEach(){} });
          def('URLSearchParams', class URLSearchParams{
            constructor(init){ this._p=[];
              if(typeof init==='string'){ var s=init.charAt(0)==='?'?init.slice(1):init; var self=this;
                if(s) s.split('&').forEach(function(kv){ if(!kv) return; var i=kv.indexOf('='); var k=i<0?kv:kv.slice(0,i); var v=i<0?'':kv.slice(i+1); self._p.push([decodeURIComponent(k),decodeURIComponent(v.replace(/\+/g,' '))]); }); }
              else if(init && typeof init.forEach==='function'){ var s2=this; init.forEach(function(v,k){ s2._p.push([k,String(v)]); }); }
              else if(init){ for(var k in init) this._p.push([k,String(init[k])]); } }
            get(n){ for(var i=0;i<this._p.length;i++) if(this._p[i][0]===n) return this._p[i][1]; return null; }
            getAll(n){ return this._p.filter(function(p){return p[0]===n;}).map(function(p){return p[1];}); }
            has(n){ return this.get(n)!==null; }
            set(n,v){ this.delete(n); this._p.push([n,String(v)]); }
            append(n,v){ this._p.push([n,String(v)]); }
            delete(n){ this._p=this._p.filter(function(p){return p[0]!==n;}); }
            forEach(cb){ var s=this; this._p.forEach(function(p){ cb(p[1],p[0],s); }); }
            keys(){ return this._p.map(function(p){return p[0];}); }
            toString(){ return this._p.map(function(p){return encodeURIComponent(p[0])+'='+encodeURIComponent(p[1]);}).join('&'); }
          });
        })();
        """;

    // The irreducible JS networking bridge over the synchronous host call __http.request: fetch hands
    // back an already-resolved Promise so .then()/await chains settle on the existing microtask drain
    // (no Task<->Promise bridging), Response.json() yields a native object via JSON.parse, and Headers/
    // Request read JS init objects. Opt-in via JsRenderOptions.EnableFetch since it issues live HTTP.
    public const string Fetch = """
        (function(){
          function toHeaderObject(h){
            var out={};
            if(!h) return out;
            if(typeof h.forEach==='function' && !Array.isArray(h)){ h.forEach(function(v,k){ out[k]=v; }); return out; }
            if(Array.isArray(h)){ for(var i=0;i<h.length;i++){ out[h[i][0]]=h[i][1]; } return out; }
            for(var k in h){ if(Object.prototype.hasOwnProperty.call(h,k)) out[k]=h[k]; }
            return out;
          }
          class Headers{
            constructor(init){ this._m={}; var o=toHeaderObject(init); for(var k in o){ this._m[String(k).toLowerCase()]=String(o[k]); } }
            get(n){ var v=this._m[String(n).toLowerCase()]; return v===undefined?null:v; }
            has(n){ return this._m[String(n).toLowerCase()]!==undefined; }
            set(n,v){ this._m[String(n).toLowerCase()]=String(v); }
            append(n,v){ var k=String(n).toLowerCase(); this._m[k]=this._m[k]!==undefined?this._m[k]+", "+v:String(v); }
            delete(n){ delete this._m[String(n).toLowerCase()]; }
            forEach(cb){ for(var k in this._m){ cb(this._m[k],k,this); } }
            keys(){ return Object.keys(this._m); }
          }
          class Response{
            constructor(r){ this._r=r; this.ok=!!r.ok; this.status=r.status; this.statusText=r.statusText||""; this.url=r.url||""; this.redirected=false; this.type="basic";
              var parsed={}; try{ parsed=JSON.parse(r.headersJson||"{}"); }catch(e){} this.headers=new Headers(parsed); this.bodyUsed=false; }
            text(){ return Promise.resolve(this._r.body||""); }
            json(){ try{ return Promise.resolve(JSON.parse(this._r.body||"null")); }catch(e){ return Promise.reject(e); } }
            clone(){ return new Response(this._r); }
          }
          class Request{
            constructor(input,init){ init=init||{}; if(input && typeof input==='object' && 'url' in input){ this.url=input.url; this.method=init.method||input.method||'GET'; this.headers=new Headers(init.headers||input.headers); this.body=init.body!==undefined?init.body:input.body; }
              else { this.url=String(input); this.method=init.method||'GET'; this.headers=new Headers(init.headers); this.body=init.body; } }
          }
          function fetch(input,init){
            init=init||{};
            // A URL host object stringifies to "[object Object]" under V8, so read its href explicitly
            // (a Request, handled below, carries .url instead). String() suffices for plain string inputs.
            if(input && typeof input==='object' && typeof input.href==='string' && typeof input.url!=='string') input=input.href;
            var url,method,headers,body;
            if(input && typeof input==='object' && 'url' in input){ url=input.url; method=init.method||input.method||'GET'; headers=init.headers||input.headers; body=init.body!==undefined?init.body:input.body; }
            else { url=String(input); method=init.method||'GET'; headers=init.headers; body=init.body; }
            var r=__http.request(url,method,JSON.stringify(toHeaderObject(headers)),body==null?null:String(body));
            if(r.error) return Promise.reject(new TypeError(r.error));
            return Promise.resolve(new Response(r));
          }
          class XMLHttpRequest{
            constructor(){ this.readyState=0; this.status=0; this.statusText=""; this.responseText=""; this.response=""; this._h={}; this._rh="{}"; this._method="GET"; this._url=""; this.onreadystatechange=null; this.onload=null; this.onerror=null; this.onloadend=null; }
            open(m,u){ this._method=m; this._url=u; this.readyState=1; if(this.onreadystatechange)this.onreadystatechange(); }
            setRequestHeader(k,v){ this._h[k]=v; }
            send(body){
              var r=__http.request(this._url,this._method,JSON.stringify(this._h),body==null?null:String(body));
              if(r.error){ this.status=0; this.readyState=4; if(this.onerror)this.onerror(new Error(r.error)); if(this.onloadend)this.onloadend(); return; }
              this.status=r.status; this.statusText=r.statusText||""; this.responseText=r.body; this.response=r.body; this._rh=r.headersJson||"{}";
              this.readyState=4; if(this.onreadystatechange)this.onreadystatechange(); if(this.onload)this.onload(); if(this.onloadend)this.onloadend();
            }
            abort(){}
            getResponseHeader(n){ try{ var o=JSON.parse(this._rh); var v=o[n]; return v===undefined?null:v; }catch(e){ return null; } }
            getAllResponseHeaders(){ try{ var o=JSON.parse(this._rh); var s=""; for(var k in o){ s+=k+": "+o[k]+"\r\n"; } return s; }catch(e){ return ""; } }
            addEventListener(t,cb){ if(t==='load')this.onload=cb; else if(t==='error')this.onerror=cb; else if(t==='loadend')this.onloadend=cb; else if(t==='readystatechange')this.onreadystatechange=cb; }
            removeEventListener(){}
          }
          XMLHttpRequest.UNSENT=0; XMLHttpRequest.OPENED=1; XMLHttpRequest.HEADERS_RECEIVED=2; XMLHttpRequest.LOADING=3; XMLHttpRequest.DONE=4;
          globalThis.Headers=globalThis.Headers||Headers;
          globalThis.Response=globalThis.Response||Response;
          globalThis.Request=globalThis.Request||Request;
          globalThis.fetch=globalThis.fetch||fetch;
          globalThis.XMLHttpRequest=globalThis.XMLHttpRequest||XMLHttpRequest;
        })();
        """;
}
