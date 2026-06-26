namespace Crawler.AngleSharp.Js.Abstractions;

public interface IModuleFetcher
{
    string? Fetch(Uri absolute);
}
