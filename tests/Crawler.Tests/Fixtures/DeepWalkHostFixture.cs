using Crawler.AngleSharp.Js.Models;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class DeepWalkHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5293/";
    public static readonly Uri HostUri = new(HostName);

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        return [ProbeSpaWebApplicationFactory.Create(HostName, "Deep Walk", _body)];
    }

    // Default options on purpose: the Object.keys(node) === [] invariant the deep-walk relies on holds for
    // the plain node wrappers, not the dynamic expando wrappers EnableDomExpandos swaps in.
    protected override JsRenderOptions CreateRenderOptions() => new();

    // Renders only if a DOM node reports no own enumerable keys (the browser invariant a deep-walking bundle
    // relies on), then runs a real JSON.stringify over it. Guards the Jint StackOverflow where node-wrapper
    // CLR getters were reported as enumerable own keys and a walker followed the DOM's cycles forever
    // (ewheels.se). The guard returns before JSON.stringify on regression, so it fails as an empty crawl.
    private const string _body = """
        <script>
            (function () {
                var node = document.createElement('div');
                var child = document.createElement('span');
                node.appendChild(child);

                if (Object.keys(node).length !== 0) return;
                JSON.stringify(node);

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

    // The shell emits links only if a DOM node reports no enumerable own keys (the browser invariant a
    // deep-walking bundle relies on), so the expected set is the JSON the shell embeds; a matching crawl
    // proves the wrapper enumeration policy holds and the walk terminates.
    protected override List<string> GetLinks()
    {
        var json = ResourceHelper.GetJsonResponse("default");
        return LinkAssertions.GetJsonLinks(HostUri, json);
    }
}
