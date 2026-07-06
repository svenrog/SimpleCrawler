namespace Crawler.Core.Robots;

/// <summary>
/// A URI path that is normalized so that it can be compared with rules in a robots.txt file
/// </summary>
public class UriPath
{
    private readonly string _value;

    public UriPath(string value)
    {
        if (PathHelpers.IsUrlEncoded(value))
        {
            _value = PathHelpers.ConvertToUtf16ForComparison(value);
        }
        else
        {
            _value = value;
        }
    }

    public string Value => _value;
    public int Length => _value.Length;
}

