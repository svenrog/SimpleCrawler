namespace SimpleCrawler.Js.Abstractions;

public interface IModuleFetcher
{
    string? Fetch(Uri absolute);

    /// <summary>
    /// The map the page published for its bare specifiers, or <c>null</c> for a page that published none. It
    /// is read off the shell, so it exists only once the document has been parsed — every module resolves
    /// after that.
    /// </summary>
    ImportMap? ImportMap => null;
}
