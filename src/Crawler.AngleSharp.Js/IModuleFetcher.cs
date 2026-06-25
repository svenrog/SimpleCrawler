namespace Crawler.AngleSharp.Js;

public interface IModuleFetcher
{
    string? Fetch(Uri absolute);
}
