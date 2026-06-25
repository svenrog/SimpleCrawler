namespace Crawler.AngleSharp.Js;

internal sealed class HttpModuleFetcher : IModuleFetcher
{
    private readonly HttpClient _client;
    private readonly CancellationToken _cancellationToken;

    public HttpModuleFetcher(HttpClient client, CancellationToken cancellationToken)
    {
        _client = client;
        _cancellationToken = cancellationToken;
    }

    // The engines resolve module imports synchronously, so we block on the synchronous
    // HttpClient.Send rather than risk sync-over-async deadlocks on GetAsync.
    public string? Fetch(Uri absolute)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, absolute);
        using var response = _client.Send(request, _cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var stream = response.Content.ReadAsStream(_cancellationToken);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
