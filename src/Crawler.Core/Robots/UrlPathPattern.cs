namespace Crawler.Core.Robots;

/// <summary>
/// Represents a URL path pattern in a robots.txt file
/// </summary>
public sealed class UrlPathPattern
{
    private readonly bool _matchSubPaths;
    private readonly string[] _patternParts;

    public UrlPathPattern(string value)
    {
        Length = value.Length;

        if (value.EndsWith('$')) value = value[..^1];
        else _matchSubPaths = true;

        var parts = value.Split('*', StringSplitOptions.None);

        if (PathHelpers.IsUrlEncoded(value))
        {
            _patternParts = [.. parts.Select(PathHelpers.ConvertToUtf16ForComparison)];
        }
        else
        {
            _patternParts = parts;
        }
    }

    /// <summary>
    /// Length of the normalized URL path pattern
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Checks if a path matches the URL rule
    /// </summary>
    /// <param name="path">The URL path</param>
    /// <returns>True if the path matches or is a sub-path; otherwise false</returns>
    public bool Matches(UriPath path)
    {
        if (Length == 0 || !path.Value.StartsWith(_patternParts[0], StringComparison.Ordinal))
            return false;

        var currentIndex = _patternParts[0].Length;

        for (var x = 1; x < _patternParts.Length; x++)
        {
            var matchIndex = path.Value.IndexOf(_patternParts[x], currentIndex);
            if (matchIndex == -1)
                return false;

            currentIndex = matchIndex + _patternParts[x].Length;
        }

        return _matchSubPaths || currentIndex == path.Length;
    }
}