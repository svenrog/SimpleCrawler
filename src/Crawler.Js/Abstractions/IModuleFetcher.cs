namespace Crawler.Js.Abstractions;

public interface IModuleFetcher
{
    string? Fetch(Uri absolute);
}
