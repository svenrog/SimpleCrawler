namespace SimpleCrawler.Core.Browser;

public sealed class DefaultBrowserProfile : IBrowserProfile
{
    public string UserAgent { get; set; } = Defaults.UserAgent;

    public string Locale => Defaults.Locale;

    public string Accept => Defaults.Accept;

    public string AcceptLanguage => Defaults.AcceptLanguage;

    public Dictionary<string, string> AdditionalHeaders => Defaults.AdditionalHeaders;

    public bool Impersonate => false;
}
