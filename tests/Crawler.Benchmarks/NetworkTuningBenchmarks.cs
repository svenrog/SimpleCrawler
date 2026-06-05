using BenchmarkDotNet.Attributes;
using Crawler.Core;
using Crawler.Core.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Crawler.Benchmarks;

// Both clients share the same connection budget, so HTTP/2 multiplexing is the only variable.
// Loopback has no bandwidth limit, so this measures the multiplexing lever only - compression
// and transfer-size savings from the tuning cannot surface here.
[MemoryDiagnoser]
[ShortRunJob]
public class NetworkTuningBenchmarks
{
    private const int _port = 5230;
    private const int _latencyMs = 20;
    private const int _connectionBudget = 4;
    private const int _requestCount = 64;

    private static readonly string _payload = BuildPayload();

    private WebApplication _host;
    private HttpClient _http11;
    private HttpClient _http2;
    private string _baseUrl;

    [GlobalSetup]
    public async Task Setup()
    {
        _baseUrl = $"https://localhost:{_port}";

        var certificate = CreateSelfSignedCertificate();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(_port, listen =>
            {
                listen.UseHttps(certificate);
                listen.Protocols = HttpProtocols.Http1AndHttp2;
            });
        });

        _host = builder.Build();
        _host.Use(async (context, next) =>
        {
            await Task.Delay(_latencyMs);
            await next(context);
        });
        _host.MapGet("/{*path}", () => Results.Content(_payload, "text/html"));

        await _host.StartAsync();

        _http11 = CreateBaselineClient();
        _http2 = CreateTunedClient();
    }

    private static HttpClient CreateBaselineClient()
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = _connectionBudget,
            SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
        };

        return new HttpClient(handler)
        {
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
    }

    private static HttpClient CreateTunedClient()
    {
        var options = new CrawlerOptions { Parallelism = _connectionBudget };

        var handler = ConfigurationHelper.CreatePrimaryHandler(options);
        handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        var client = new HttpClient(handler);
        ConfigurationHelper.ConfigureClient(client, options);

        return client;
    }

    [Benchmark(Baseline = true)]
    public Task Http11()
    {
        return Fetch(_http11);
    }

    [Benchmark]
    public Task Http2()
    {
        return Fetch(_http2);
    }

    private async Task Fetch(HttpClient client)
    {
        var tasks = new Task[_requestCount];
        for (var i = 0; i < _requestCount; i++)
            tasks[i] = Get(client, i);

        await Task.WhenAll(tasks);
    }

    private async Task Get(HttpClient client, int index)
    {
        using var response = await client.GetAsync($"{_baseUrl}/page/{index}");
        _ = await response.Content.ReadAsByteArrayAsync();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _http11.Dispose();
        _http2.Dispose();
        await _host.DisposeAsync();
    }

    private static string BuildPayload()
    {
        var links = string.Concat(Enumerable.Range(0, 50).Select(i => $"<a href=\"/page/{i}\">{i}</a>"));
        return $"<!doctype html><html><head><link rel=\"canonical\" href=\"/\" /></head><body>{links}</body></html>";
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        request.CertificateExtensions.Add(sanBuilder.Build());

        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
    }
}
