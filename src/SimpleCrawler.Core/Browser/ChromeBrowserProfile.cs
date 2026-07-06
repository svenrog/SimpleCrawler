namespace SimpleCrawler.Core.Browser;

public class ChromeBrowserProfile : IBrowserProfile
{
    private readonly Version _version;
    private readonly string _userAgent;

    public ChromeBrowserProfile(string version = "140.0.0.0")
    {
        _version = Version.Parse(version);
        _userAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            $"(KHTML, like Gecko) Chrome/{_version} Safari/537.36";

        AdditionalHeaders = new Dictionary<string, string>()
        {
            { "Sec-CH-UA", $"\"Chromium\";v=\"{_version.Major}\", \"Google Chrome\";v=\"{_version.Major}\", \"Not?A_Brand\";v=\"24\"" },
            { "Sec-CH-UA-Mobile", "?0" },
            { "Sec-CH-UA-Platform", "\"Windows\"" },
            { "Sec-Fetch-Site", "none" },
            { "Sec-Fetch-Mode", "navigate" },
            { "Sec-Fetch-User", "?1" },
            { "Sec-Fetch-Dest", "document" },
            { "Upgrade-Insecure-Requests", "1" },
        };
    }

    public string UserAgent => _userAgent;

    public string Locale { get; set; } = Defaults.Locale;

    public string Accept { get; set; } = Defaults.Accept;

    public string AcceptLanguage { get; set; } = Defaults.AcceptLanguage;

    public Dictionary<string, string> AdditionalHeaders { get; }

    public bool Impersonate => true;
}
