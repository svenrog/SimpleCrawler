namespace SimpleCrawler.Core.Browser;

public sealed record DefaultBrowserProfile : IBrowserProfile
{
    public string UserAgent { get; set; } = Defaults.UserAgent;

    public string Locale => Defaults.Locale;

    public string Accept => Defaults.Accept;

    public string AcceptLanguage => Defaults.AcceptLanguage;

    public Dictionary<string, string> AdditionalHeaders { get; } = [];

    public bool Impersonate => false;
}
