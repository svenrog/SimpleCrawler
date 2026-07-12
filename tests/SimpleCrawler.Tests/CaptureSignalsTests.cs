using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.AngleSharp;
using SimpleCrawler.Core.Models;
using SimpleCrawler.HtmlAgilityPack;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.V8;
using SimpleCrawler.Playwright;
using SimpleCrawler.Puppeteer;
using SimpleCrawler.Tests.Fixtures;

namespace SimpleCrawler.Tests;

/// <summary>
/// Guards --captureSignals end to end on every backend: static (AngleSharp/HtmlAgilityPack), pure-JS
/// (Jint/V8), and headless (Playwright/Puppeteer) all populate the same PageSignals shape from the
/// same page.
/// </summary>
[Collection("Crawler")]
public class CaptureSignalsTests : IClassFixture<SignalsHostFixture>
{
    private readonly SignalsHostFixture _context;

    public CaptureSignalsTests(SignalsHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task AngleSharpCrawler_Captures_Signals()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpCrawler>();
        var result = await subject.Start(SignalsHostFixture.HostName, TestContext.Current.CancellationToken);
        AssertSignals(result);
    }

    [Fact]
    public async Task HtmlAgilityPackCrawler_Captures_Signals()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultHtmlAgilityPackCrawler>();
        var result = await subject.Start(SignalsHostFixture.HostName, TestContext.Current.CancellationToken);
        AssertSignals(result);
    }

    [Fact]
    public async Task JintCrawler_Captures_Signals()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultJintCrawler>();
        var result = await subject.Start(SignalsHostFixture.HostName, TestContext.Current.CancellationToken);
        AssertSignals(result);
    }

    [Fact]
    public async Task V8Crawler_Captures_Signals()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultV8Crawler>();
        var result = await subject.Start(SignalsHostFixture.HostName, TestContext.Current.CancellationToken);
        AssertSignals(result);
    }

    [Fact]
    public async Task PlaywrightCrawler_Captures_Signals()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultPlaywrightCrawler>();
        var result = await subject.Start(SignalsHostFixture.HostName, TestContext.Current.CancellationToken);
        AssertSignals(result);
    }

    [Fact]
    public async Task PuppeteerCrawler_Captures_Signals()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultPuppeteerCrawler>();
        var result = await subject.Start(SignalsHostFixture.HostName, TestContext.Current.CancellationToken);
        AssertSignals(result);
    }

    private static void AssertSignals(IScrapeResult result)
    {
        var root = Assert.Single(result.Reports, r => r.Url == SignalsHostFixture.HostName);

        Assert.NotNull(root.Signals);
        var signals = root.Signals!;

        Assert.True(signals.Headers.ContainsKey("content-type"));
        Assert.Contains("session", signals.CookieNames);
        Assert.Contains("/app.js", signals.ScriptSources);
        Assert.Contains(signals.JsonLdBlocks, block => block.Contains("Organization"));
        Assert.Equal("index, follow", signals.MetaTags.GetValueOrDefault("robots"));
        Assert.Equal("SimpleCrawler test host", signals.MetaTags.GetValueOrDefault("generator"));
    }
}
