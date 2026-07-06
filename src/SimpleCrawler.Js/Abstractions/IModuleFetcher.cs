namespace SimpleCrawler.Js.Abstractions;

public interface IModuleFetcher
{
    string? Fetch(Uri absolute);
}
