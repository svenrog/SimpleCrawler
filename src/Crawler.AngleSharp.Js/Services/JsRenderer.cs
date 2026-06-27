using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom;
using Crawler.AngleSharp.Js.Dom.Network;
using Crawler.AngleSharp.Js.Dom.Observers;
using Crawler.AngleSharp.Js.Dom.Window;
using Crawler.AngleSharp.Js.Errors;
using Crawler.AngleSharp.Js.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Crawler.AngleSharp.Js.Services;

public sealed class JsRenderer
{
    private const int _idleTurnsBeforeSettled = 3;

    private static readonly HtmlParser _parser = new();
    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IJsEngineFactory _engineFactory;
    private readonly JsRenderOptions _options;
    private readonly ILogger _logger;
    private readonly SourceCache _sources = new();

    public JsRenderer(IJsEngineFactory engineFactory, JsRenderOptions options, ILogger logger)
    {
        _engineFactory = engineFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<byte[]> RenderAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
    {
        // A shell with no <script> at all cannot render anything, and the caller re-parses these same
        // bytes to extract links — so skip the parse, the engine, and the reserialize entirely.
        if (!ContainsScriptTag(shell))
            return shell;

        var pageUri = new Uri(pageUrl);
        using var stream = new MemoryStream(shell, writable: false);
        using var document = await _parser.ParseDocumentAsync(stream, cancellationToken);

        var (regularScripts, moduleEntries) = await CollectScriptsAsync(document, pageUrl, client, _sources, cancellationToken);

        // The markup had a <script>, but none were executable (e.g. JSON, importmap), so the DOM still
        // equals the shell: skip spinning up a JS engine (a fresh V8 isolate / Jint engine) and reserializing.
        if (regularScripts.Count == 0 && moduleEntries.Count == 0)
            return shell;

        var fetcher = new HttpModuleFetcher(client, _sources, cancellationToken);
        using var engine = _engineFactory.Create(fetcher, pageUri);
        var context = new DomContext(document, engine, pageUri, _options.EnableDomExpandos);

        if (_options.EnableFetch)
            engine.EmbedHostObject("__http", new JsHttp(client, pageUri, _logger, cancellationToken));

        SetupGlobals(engine, context, _options.EnableFetch);

        foreach (var script in regularScripts)
            RunRegular(engine, context, script, pageUrl);

        foreach (var module in moduleEntries)
            RunModule(engine, module, pageUrl);

        Drain(engine, context, pageUri, client, pageUrl, cancellationToken);

        return Serialize(document.DocumentElement);
    }

    // Stream the rendered tree straight to UTF-8 bytes rather than materializing OuterHtml first: a
    // rendered SPA page is large enough that the intermediate string would be a per-page LOH allocation.
    private static byte[] Serialize(IElement? root)
    {
        if (root is null)
            return [];

        using var buffer = new MemoryStream();
        using (var writer = new StreamWriter(buffer, _utf8NoBom, leaveOpen: true))
            root.ToHtml(writer, HtmlMarkupFormatter.Instance);

        return buffer.ToArray();
    }

    private static void SetupGlobals(IJsEngine engine, DomContext context, bool enableFetch)
    {
        engine.EmbedHostObject("document", context.DocumentWrapper);
        engine.EmbedHostObject("location", context.Location);
        engine.EmbedHostObject("__history", context.History);
        engine.EmbedHostObject("navigator", context.Navigator);
        engine.EmbedHostObject("localStorage", context.LocalStorage);
        engine.EmbedHostObject("sessionStorage", context.SessionStorage);
        engine.EmbedHostObject("__crypto", context.Crypto);
        engine.EmbedHostObject("customElements", context.CustomElements);
        engine.EmbedHostObject("console", context.Console);
        engine.EmbedHostObject("performance", context.Performance);
        engine.EmbedHostType("IntersectionObserver", typeof(JsIntersectionObserver));
        engine.EmbedHostType("ResizeObserver", typeof(JsResizeObserver));
        engine.EmbedHostType("MutationObserver", typeof(JsMutationObserver));
        engine.EmbedHostType("TextEncoder", typeof(JsTextEncoder));
        engine.EmbedHostType("TextDecoder", typeof(JsTextDecoder));

        // A CLR host type embedded with AddHostType has no JS .prototype, so `x instanceof Element` throws
        // on V8 ("Function has non-object prototype undefined") rather than testing the wrapper's type.
        // So URL/Event (constructed) are embedded privately and re-exposed as JS shims that construct via
        // the host type and carry a Symbol.hasInstance, while Node/Element/Text/Document (only ever an
        // instanceof right-hand side, never `new`'d) are plain JS shims. __isInstance answers by CLR type.
        engine.EmbedHostType("__ctor_URL", typeof(JsUrl));
        engine.EmbedHostType("__ctor_Event", typeof(JsEvent));
        engine.EmbedHostType("__ctor_CustomEvent", typeof(JsCustomEvent));

        var bridge = context.Bridge;
        EmbedGlobalFunction(engine, "__isInstance", bridge.IsInstance);
        engine.Execute(_instanceShimsPrelude);

        EmbedGlobalFunction(engine, "matchMedia", bridge.MatchMedia);
        EmbedGlobalFunction(engine, "getComputedStyle", bridge.GetComputedStyle);
        EmbedGlobalFunction(engine, "setTimeout", bridge.SetTimeout);
        EmbedGlobalFunction(engine, "clearTimeout", bridge.Noop);
        EmbedGlobalFunction(engine, "setInterval", bridge.SetInterval);
        EmbedGlobalFunction(engine, "clearInterval", bridge.Noop);
        EmbedGlobalFunction(engine, "requestAnimationFrame", bridge.RequestAnimationFrame);
        EmbedGlobalFunction(engine, "cancelAnimationFrame", bridge.Noop);
        EmbedGlobalFunction(engine, "queueMicrotask", bridge.QueueMicrotask);
        EmbedGlobalFunction(engine, "addEventListener", bridge.Noop);
        EmbedGlobalFunction(engine, "removeEventListener", bridge.Noop);
        EmbedGlobalFunction(engine, "dispatchEvent", bridge.ReturnTrue);

        // The bundle reaches the DOM through window/self; both are just the global object here.
        // structuredClone has no host equivalent, but the bundle only clones plain data, so a JSON
        // round-trip stands in (guarded so a native implementation, if present, wins).
        engine.Execute(
            "var window=globalThis;var self=globalThis;" +
            "globalThis.structuredClone=globalThis.structuredClone||function(v){return v===undefined?undefined:JSON.parse(JSON.stringify(v));};");

        // document.defaultView must return the same `window` the bundle reads through globalThis: history
        // libraries default `let {window = document.defaultView} = opts` then read window.history, so a null
        // defaultView crashes them with "Cannot read properties of null (reading 'history')".
        context.Window = engine.GetGlobalObject();

        // crypto is a JS object (not the host wrapper directly) because uuid/nanoid bundles do
        // crypto.randomUUID.bind(crypto), and a V8/ClearScript host method is not a real JS function
        // (no .bind/.call). randomUUID delegates to the host for a real GUID; getRandomValues fills in JS.
        engine.Execute(
            "globalThis.crypto=globalThis.crypto||{" +
            "randomUUID:function(){return __crypto.randomUUID();}," +
            "getRandomValues:function(a){if(a)for(var i=0;i<a.length;i++)a[i]=Math.floor(Math.random()*256);return a;}};");

        // The renderer fires load/error on a dynamically appended <script>/<link> by invoking the handler
        // the bundle stashed on the node (an expando) with a matching event object — see FireResourceEvent.
        engine.Execute("globalThis.__invokeResourceEvent=function(h,t){if(typeof h==='function')h({type:t});};");

        // MessageChannel is how React's scheduler and state-batching helpers defer a flush ("if MessageChannel
        // is defined, port2.postMessage triggers port1.onmessage" — else a fallback that never runs here). Each
        // postMessage delivers to the paired port's onmessage as a macrotask via our setTimeout drain, so those
        // deferred flushes (which commit data-driven render updates) actually happen. instanceof type, stays JS.
        engine.Execute(_messageChannelPrelude);

        // history is a plain JS object delegating to the host wrapper rather than the host object itself,
        // because routers (React Router's history lib) reassign history.pushState/replaceState and set
        // history.scrollRestoration — assignments a CLR host object rejects as read-only members.
        engine.Execute(
            "(function(){var h=__history;globalThis.history={" +
            "get length(){return h.length;},get state(){return h.state;},scrollRestoration:'auto'," +
            "pushState:function(s,t,u){return h.pushState(s,t,u);}," +
            "replaceState:function(s,t,u){return h.replaceState(s,t,u);}," +
            "go:function(d){return h.go(d);},back:function(){return h.back();},forward:function(){return h.forward();}};})();");

        // HTMLElement is the one DOM global that bundles *extend* (`class X extends HTMLElement`) rather
        // than construct, and V8/ClearScript can't `class extends` a CLR host type (its host objects have
        // no JS prototype) — so unlike Event/CustomEvent above it has to be a real JS class. It is never
        // instantiated (customElements.define is a no-op), so the body is just no-op stubs.
        engine.Execute(
            "globalThis.HTMLElement=globalThis.HTMLElement||class HTMLElement{" +
            "addEventListener(){}removeEventListener(){}dispatchEvent(){return true;}attachShadow(){return this;}};" +
            "globalThis.HTMLScriptElement=globalThis.HTMLScriptElement||class HTMLScriptElement extends HTMLElement{};");

        // Remaining DOM/Web globals the bundle uses only as instanceof right-hand sides (no host wrapper
        // models them, so the check is always false — which is the correct answer for a crawl). They have
        // to exist as objects or instanceof throws. URLSearchParams is given a working body because the
        // router actually parses query strings with it. Per project convention instanceof types stay JS.
        engine.Execute(_domGlobalsPrelude);

        if (enableFetch)
        {
            // AbortController/AbortSignal are plain no-op host objects (a synchronous render never aborts);
            // the rest of the surface stays in JS because it must return native Promises, read arbitrary
            // JS init objects, or invoke JS callbacks — none of which a host object does identically across
            // Jint and ClearScript.
            engine.EmbedHostType("AbortController", typeof(JsAbortController));
            engine.EmbedHostType("AbortSignal", typeof(JsAbortSignal));
            engine.Execute(_fetchPrelude);
        }
    }

    // ClearScript/V8 exposes an embedded host delegate as a non-writable global that is NOT a real JS
    // Function — it has no .bind/.call (React's scheduler does `requestAnimationFrame.bind(...)`), and a
    // bundle's `globalThis.x = wrapper` reassignment silently fails. So embed the delegate under a private
    // name and expose the public global as a writable, real JS function that spreads into it. (Jint's
    // delegate wrapper already is a JS function, but the indirection is harmless there.)
    private static void EmbedGlobalFunction(IJsEngine engine, string name, VFunc function)
    {
        engine.EmbedFunction("__fn_" + name, function);
        engine.Execute($"globalThis.{name}=function(){{return __fn_{name}(...arguments);}};");
    }

    // See the embedding site: CLR host types have no JS prototype, so the DOM/Web globals bundles use as an
    // instanceof right-hand side are JS shims whose Symbol.hasInstance defers to the host type check. URL and
    // Event also stay constructible by forwarding `new` to the privately embedded host type.
    private const string _instanceShimsPrelude = """
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

    private const string _messageChannelPrelude = """
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

    private const string _domGlobalsPrelude = """
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
    private const string _fetchPrelude = """
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

    private void Drain(IJsEngine engine, DomContext context, Uri pageUri, HttpClient client, string pageUrl, CancellationToken cancellationToken)
    {
        // The bundle defers work onto setTimeout/requestAnimationFrame (drained from our queue), native
        // promise jobs (dynamic import for lazy routes, drained at each RunMicrotasks boundary), and
        // <script src> chunks it appends to the DOM (fetched and executed here so their module
        // registrations run and the awaiting import() resolves). V8 resolves dynamic import() on those
        // boundaries rather than synchronously like Jint, so we keep pumping through empty turns until
        // the queue has stayed idle for a few consecutive turns.
        var iterations = 0;
        var idle = 0;
        while (iterations++ < _options.MaxTaskDrainIterations && idle < _idleTurnsBeforeSettled)
        {
            var resources = context.TakePendingResources();
            foreach (var resource in resources)
                LoadResource(engine, context, resource, pageUri, client, pageUrl, cancellationToken);

            var batch = context.TakeTasks();
            foreach (var callback in batch)
            {
                try
                {
                    engine.InvokeCallback(callback);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Task callback error on '{url}': {message}", pageUrl, ex.Message);
                }
            }

            engine.RunMicrotasks();
            idle = resources.Count == 0 && batch.Count == 0 && context.PendingTaskCount == 0 && context.PendingResourceCount == 0
                ? idle + 1
                : 0;
        }
    }

    // A <script>/<link> the bundle appended at runtime. Same-origin scripts are fetched and executed (so a
    // webpack chunk's module registrations run); a <link> is treated as loaded without fetching, since a
    // crawl needs no CSS. Either way the resource's load event fires to settle the awaiting import(). The
    // cross-origin scripts (AppInsights/GTM/Flowbox) are analytics SDKs irrelevant to a crawl, so they are
    // left pending — running them would be slow and produce no links.
    private void LoadResource(IJsEngine engine, DomContext context, IElement resource, Uri pageUri, HttpClient client, string pageUrl, CancellationToken cancellationToken)
    {
        if (resource is IHtmlLinkElement)
        {
            FireResourceEvent(engine, context, resource, "onload");
            return;
        }

        var src = resource.GetAttribute("src");
        if (string.IsNullOrEmpty(src) || !Uri.TryCreate(pageUri, src, out var absolute))
            return;

        if (!string.Equals(absolute.Host, pageUri.Host, StringComparison.OrdinalIgnoreCase))
            return;

        var source = FetchSourceAsync(client, _sources, absolute, cancellationToken).GetAwaiter().GetResult();
        if (source is null)
        {
            FireResourceEvent(engine, context, resource, "onerror");
            return;
        }

        try
        {
            engine.Execute(source);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Chunk execution error on '{url}': {message}", pageUrl, ex.Message);
            FireResourceEvent(engine, context, resource, "onerror");
            return;
        }

        FireResourceEvent(engine, context, resource, "onload");
    }

    // webpack/React resolve a chunk's CSS (and script) promise from the resource's load event, checking
    // event.type === 'load'; the handler lives in the node's expando table (it is assigned, not a real
    // member). __invokeResourceEvent builds that event and calls it. The chunk's own push() settles the
    // JS half, but the CSS half only settles here — without it a code-split route's import() never resolves.
    private static void FireResourceEvent(IJsEngine engine, DomContext context, IElement resource, string handler)
    {
        if (context.TryGetExpando(resource, handler, out var callback) && callback is not null)
        {
            try
            {
                engine.CallGlobal("__invokeResourceEvent", callback, handler == "onload" ? "load" : "error");
            }
            catch
            {
                // A missing/odd handler must not abort the drain; the chunk itself already executed.
            }
        }
    }

    private void RunRegular(IJsEngine engine, DomContext context, RegularScript script, string pageUrl)
    {
        context.CurrentScript = engine.CreateScriptElement(script.Src);
        try
        {
            engine.Execute(script.Source);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Bundle execution error on '{url}': {message}\n{details}", pageUrl, ex.Message, ex.ErrorDetails);
        }
        finally
        {
            context.CurrentScript = null;
        }
    }

    private void RunModule(IJsEngine engine, ModuleScript module, string pageUrl)
    {
        try
        {
            engine.EvaluateModule(module.Specifier, module.Source);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Module execution error on '{url}': {message}", pageUrl, ex.Message);
        }
    }

    private static bool ContainsScriptTag(ReadOnlySpan<byte> html)
    {
        ReadOnlySpan<byte> marker = "<script"u8;
        var start = 0;
        while (true)
        {
            var index = html[start..].IndexOf((byte)'<');
            if (index < 0 || html.Length - (start + index) < marker.Length)
                return false;

            start += index;
            if (AsciiEqualsIgnoreCase(html.Slice(start, marker.Length), marker))
                return true;

            start++;
        }
    }

    private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> value, ReadOnlySpan<byte> lowercase)
    {
        for (var i = 0; i < lowercase.Length; i++)
        {
            var c = value[i];
            if (c >= 'A' && c <= 'Z')
                c = (byte)(c + 32);

            if (c != lowercase[i])
                return false;
        }

        return true;
    }

    private static async Task<(IReadOnlyList<RegularScript> Regular, IReadOnlyList<ModuleScript> Modules)> CollectScriptsAsync(IDocument document, string pageUrl, HttpClient client, SourceCache sources, CancellationToken cancellationToken)
    {
        var baseUri = new Uri(pageUrl);
        var regular = new List<RegularScript>();
        var modules = new List<ModuleScript>();

        foreach (var element in document.QuerySelectorAll("script"))
        {
            var script = (IHtmlScriptElement)element;
            var type = script.Type;
            if (!string.IsNullOrEmpty(type) && type is not "text/javascript" and not "module" and not "application/javascript")
                continue;

            var isModule = string.Equals(type, "module", StringComparison.Ordinal);
            var src = script.GetAttribute("src");

            if (string.IsNullOrEmpty(src))
            {
                if (string.IsNullOrEmpty(script.TextContent))
                    continue;

                if (isModule)
                    modules.Add(new ModuleScript(pageUrl, script.TextContent));
                else
                    regular.Add(new RegularScript(script.TextContent, pageUrl));

                continue;
            }

            var absolute = new Uri(baseUri, src);
            var source = await FetchSourceAsync(client, sources, absolute, cancellationToken);
            if (source is null)
                continue;

            if (isModule)
                modules.Add(new ModuleScript(absolute.ToString(), source));
            else
                regular.Add(new RegularScript(source, absolute.ToString()));
        }

        return (regular, modules);
    }

    private static async Task<string?> FetchSourceAsync(HttpClient client, SourceCache sources, Uri absolute, CancellationToken cancellationToken)
    {
        if (sources.TryGet(absolute, out var cached))
            return cached;

        using var response = await client.GetAsync(absolute, cancellationToken);
        var source = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
        return sources.Store(absolute, source);
    }
}
