using Crawler.AngleSharp.Js.Models;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

// One host per JS-engine capability probe, each on its own port, served from a single fixture (mirroring
// SpaHostFixture's multi-host layout). Every probe shell embeds the same default link manifest and only
// renders it when its capability behaves, so a crawl that matches the manifest proves the capability. The
// fixture enables the superset of opt-in render features so each probe's requirement is satisfied; the
// DeepWalk probe is intentionally separate because it asserts the default-config enumeration invariant.
public sealed class ProbeHostFixture : AbstractHostFixture
{
    private const int _basePort = 5280;

    public static string HostName(ProbeCapability capability) =>
        $"http://localhost:{_basePort + (int)capability}/";

    public static IReadOnlyList<string> LinksFor(ProbeCapability capability)
    {
        var baseUri = new Uri(HostName(capability));

        // The browser-API shell renders one /features/* link per passing probe; its manifest has no root
        // entry, so the start page the crawler always visits is added explicitly.
        if (capability == ProbeCapability.BrowserApis)
        {
            return
            [
                HostName(capability),
                .. LinkAssertions.GetJsonLinks(baseUri, ResourceHelper.GetJsonResponse("features")),
            ];
        }

        return LinkAssertions.GetJsonLinks(baseUri, ResourceHelper.GetJsonResponse("default"));
    }

    protected override JsRenderOptions CreateRenderOptions() =>
        new() { EnableFetch = true, EnableDomExpandos = true };

    protected override IEnumerable<WebApplication> CreateHosts() =>
        Enum.GetValues<ProbeCapability>().Select(CreateHost);

    private static WebApplication CreateHost(ProbeCapability capability)
    {
        var host = HostName(capability);

        return capability switch
        {
            // Runtime network fetch is structurally different (extra endpoint, no embedded links).
            ProbeCapability.Fetch => FetchSpaWebApplicationFactory.Create(host),
            _ => ProbeSpaWebApplicationFactory.Create(host, capability.ToString(), Body(capability)),
        };
    }

    private static string Body(ProbeCapability capability) => capability switch
    {
        ProbeCapability.AnchorHref => _anchorHrefBody,
        ProbeCapability.Expando => _expandoBody,
        ProbeCapability.DeferredCallback => _deferredCallbackBody,
        ProbeCapability.JQuery => _jQueryBody,
        ProbeCapability.BrowserApis => _browserApisBody,
        _ => throw new ArgumentOutOfRangeException(nameof(capability)),
    };

    // Assigns each link through the anchor `href` *property* on a freshly created anchor (a router/helmet
    // does this) and reads the reflected protocol/host/pathname back. Guards the setter that ran
    // `new Uri("")` on the element's own empty href and threw "Invalid URI: The URI is empty." (prep.öob.se).
    private const string _anchorHrefBody = """
        <script>
            (function () {
                var links = __LINKS__;
                var app = document.getElementById('app');
                for (var i = 0; i < links.length; i++) {
                    var anchor = document.createElement('a');
                    anchor.href = links[i].href;
                    if (!anchor.protocol || !anchor.host || !anchor.pathname) continue;
                    anchor.textContent = links[i].name;
                    app.appendChild(anchor);
                }
            })();
        </script>
        """;

    // Stores a cyclic expando on a DOM node (React 18 stashes fibers there) and assigns a freshly created
    // element's onload handler (webpack's chunk loader does `script.onload = fn`). Guards the expando side
    // table and onload/onerror living on the base JsElement (exposing them on the derived wrapper threw
    // MissingMemberException on V8, breaking dynamic chunk loading on nille.no).
    private const string _expandoBody = """
        <script>
            (function () {
                var probe = document.createElement('div');
                var cyclic = {}; cyclic.self = cyclic;
                probe.__fiber = cyclic;
                document.__root = probe;
                var script = document.createElement('script');
                script.onload = function () { };
                var ok = probe.__fiber === cyclic
                    && probe.__fiber.self === probe.__fiber
                    && document.__root === probe
                    && typeof script.onload === 'function';
                if (!ok) return;
                var links = __LINKS__;
                var app = document.getElementById('app');
                for (var i = 0; i < links.length; i++) {
                    var anchor = document.createElement('a');
                    anchor.setAttribute('href', links[i].href);
                    anchor.textContent = links[i].name;
                    app.appendChild(anchor);
                }
            })();
        </script>
        """;

    // Renders from inside a setTimeout callback whose callee binds a destructuring parameter with a default
    // that reads outer scope. Guards Jint deferred callbacks running through the engine's evaluation context
    // (the raw marshalled Func delegate threw a bare NullReferenceException on the default; blanked nille.no).
    private const string _deferredCallbackBody = """
        <script>
            (function () {
                var data = __LINKS__;
                function render({ items = data } = {}) {
                    var app = document.getElementById('app');
                    for (var i = 0; i < items.length; i++) {
                        var anchor = document.createElement('a');
                        anchor.setAttribute('href', items[i].href);
                        anchor.textContent = items[i].name;
                        app.appendChild(anchor);
                    }
                }
                setTimeout(function () { render(); }, 0);
            })();
        </script>
        """;

    // Mimics jQuery's UMD: a load-time IIFE runs feature detection against the DOM, then assigns
    // window.jQuery; a later "webpack external" reads the global to render. Guards the surface jQuery touches
    // during init (createDocumentFragment, implementation.createHTMLDocument, loosely reflected script
    // async/defer/type) — missing any of it threw before the assignment, so later bundles failed with
    // "jQuery is not defined" (ewheels.se).
    private const string _jQueryBody = """
        <script>
            (function (global, factory) {
                global.jQuery = global.$ = factory(global);
            })(window, function (window) {
                var fragment = document.createDocumentFragment();
                var support = document.implementation.createHTMLDocument('');
                var script = document.createElement('script');
                script.async = 1;
                script.defer = 1;
                script.type = 'text/javascript';
                if (!fragment || !support || !script.async || !script.defer || script.type !== 'text/javascript')
                    throw new Error('jQuery feature detection failed');
                return { fn: { jquery: '3.x' } };
            });
        </script>
        <script>
            (function () {
                if (typeof jQuery === 'undefined' || window.$ !== jQuery) return;
                var links = __LINKS__;
                var app = document.getElementById('app');
                for (var i = 0; i < links.length; i++) {
                    var anchor = document.createElement('a');
                    anchor.setAttribute('href', links[i].href);
                    anchor.textContent = links[i].name;
                    app.appendChild(anchor);
                }
            })();
        </script>
        """;

    // Appends one /features/* link per browser API the engine implements (each probe ported from the test
    // host's former Astro feature-nav, which contrived shared code didn't belong in the real-framework
    // SPAs). Asserting all of them render guards the navigator/storage/observer/crypto/customElements/cookie
    // surface the JS render engines must expose for production bundles.
    private const string _browserApisBody = """
        <script>
            (function () {
                var features = __FEATURES__;
                var probes = {
                    'geolocation': function () {
                        return !!(navigator.geolocation && typeof navigator.geolocation.getCurrentPosition === 'function');
                    },
                    'local-storage': function () {
                        localStorage.setItem('crawler-probe', 'ok');
                        var ok = localStorage.getItem('crawler-probe') === 'ok';
                        localStorage.removeItem('crawler-probe');
                        return ok;
                    },
                    'intersection-observer': function () {
                        var observer = new IntersectionObserver(function () { });
                        observer.observe(document.documentElement);
                        observer.disconnect();
                        return true;
                    },
                    'cookies': function () {
                        document.cookie = 'crawler-probe=ok; path=/';
                        return document.cookie.indexOf('crawler-probe=ok') !== -1;
                    },
                    'session-storage': function () {
                        sessionStorage.setItem('crawler-probe', 'ok');
                        var ok = sessionStorage.getItem('crawler-probe') === 'ok';
                        sessionStorage.removeItem('crawler-probe');
                        return ok;
                    },
                    'match-media': function () {
                        if (typeof window.matchMedia !== 'function') return false;
                        return typeof window.matchMedia('(min-width: 0px)').matches === 'boolean';
                    },
                    'resize-observer': function () {
                        var observer = new ResizeObserver(function () { });
                        observer.observe(document.documentElement);
                        observer.disconnect();
                        return true;
                    },
                    'mutation-observer': function () {
                        var observer = new MutationObserver(function () { });
                        observer.observe(document.documentElement, { childList: true });
                        observer.disconnect();
                        return true;
                    },
                    'structured-clone': function () {
                        return structuredClone({ ok: true }).ok === true;
                    },
                    'crypto-random-uuid': function () {
                        if (typeof crypto === 'undefined' || !crypto || typeof crypto.randomUUID !== 'function') return false;
                        return typeof crypto.randomUUID() === 'string';
                    },
                    'custom-elements': function () {
                        return typeof customElements !== 'undefined' && !!customElements && typeof customElements.define === 'function';
                    }
                };
                var app = document.getElementById('app');
                for (var i = 0; i < features.length; i++) {
                    var feature = features[i];
                    var probe = probes[feature.key];
                    if (!probe) continue;
                    var ok = false;
                    try { ok = probe(); } catch (e) { ok = false; }
                    if (!ok) continue;
                    var anchor = document.createElement('a');
                    anchor.setAttribute('href', feature.href);
                    anchor.textContent = feature.name;
                    app.appendChild(anchor);
                }
            })();
        </script>
        """;
}
