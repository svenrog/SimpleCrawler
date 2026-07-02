namespace Crawler.Core.Browser;

public interface IBrowserProfile
{
    string UserAgent { get; }
    string Locale { get; }
    string Accept { get; }
    string AcceptLanguage { get; }
    Dictionary<string, string> AdditionalHeaders { get; }
    bool Impersonate { get; }
}
