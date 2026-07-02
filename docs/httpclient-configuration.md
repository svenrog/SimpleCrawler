# HttpClient configuration

Static (`HtmlAgilityPack`, `AngleSharp`) and JS (`Jint`, `V8`) backends take an optional
`Action<IServiceProvider, HttpClient>` hook on their `Add…Crawler` call. Use it to attach
credentials, cookies, or any custom header to the outgoing requests.

```csharp
services.AddHtmlAgilityPackCrawler(options, (provider, client) =>
{
    // runs against the HttpClient before the crawler applies its own defaults
});
```

Two things worth knowing:

- The hook is applied to **both** the page client and the internal `robots.txt`/sitemap client, so a site that puts those behind the same auth is fetched with the same credentials.
- It runs *before* the crawler sets its defaults (HTTP/2, and the headers from the
  [`BrowserProfile`](./crawler-options.md#browserprofile)). A `User-Agent` (or any profile header) you set in the hook is kept — the crawler won't overwrite one that's already present.

## Cookies

```csharp
services.AddHtmlAgilityPackCrawler(options, (provider, client) =>
    client.DefaultRequestHeaders.Add("Cookie", "session=abc123; theme=dark"));
```

The underlying handler keeps a cookie container, so `Set-Cookie` responses during the crawl are stored and resent automatically. Use the hook to seed an initial cookie (an existing logged-in session, a consent cookie), not to track per-request cookies yourself.

## Basic auth

```csharp
using System.Net.Http.Headers;
using System.Text;

services.AddHtmlAgilityPackCrawler(options, (provider, client) =>
{
    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:password"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
});
```

## Bearer token

```csharp
using System.Net.Http.Headers;

services.AddV8Crawler(options, renderOptions, config: (provider, client) =>
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token));
```

## Resolving secrets from DI

The hook hands you the `IServiceProvider`, the credential can come from configuration or any
registered service instead of being hard-coded:

```csharp
services.AddSingleton<ITokenProvider, VaultTokenProvider>();

services.AddV8Crawler(options, renderOptions, config: (provider, client) =>
{
    var token = provider.GetRequiredService<ITokenProvider>().GetToken();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
});
```

## From the CLI

The `smpcrawl` CLI wires its `--cookie` and `--userAgent` flags through this same hook. The commandline is just a thin front for the cases above.

## Headless backends

`Playwright` and `Puppeteer` don't fetch over `HttpClient` and take no hook headers, cookies, or a stored session has to be set in the browser context instead (e.g. Playwright's `ExtraHTTPHeaders`).
