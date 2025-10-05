using System.Diagnostics.CodeAnalysis;

namespace Crawler.Core.Comparers;

public sealed class CharIgnoreCaseEqualityComparer : IEqualityComparer<char>
{
    public bool Equals(char x, char y)
    {
        var xn = char.IsUpper(x) ? char.ToLower(x) : x;
        var yn = char.IsUpper(y) ? char.ToLower(y) : y;

        return xn == yn;
    }

    public int GetHashCode([DisallowNull] char obj)
    {
        var normalized = char.IsUpper(obj) ? char.ToLower(obj) : obj;

        return normalized.GetHashCode();
    }
}
