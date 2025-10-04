using System.Text;

namespace Crawler.Core.Robots;

internal static class PathHelpers
{
    private static readonly HashSet<char> _reservedChars =
    [
        ':' , '/' , '?' , '#' , '[' , ']' , '@',
        '!', '$', '&', '\'', '(', ')', '*', '+', ',', ';', '='
    ];

    /*
     * A B C D E F G H I J K L M N O P Q R S T U V W X Y Z
     * a b c d e f g h i j k l m n o p q r s t u v w x y z
     * 0 1 2 3 4 5 6 7 8 9 - _ . ~
     */
    private static readonly string[] _unreservedCharactersPercentEncoded =
    [
        "%41", "%42", "%43", "%44", "%45", "%46", "%47", "%48", "%49", "%4A", "%4B", "%4C", "%4D", "%4E", "%4F", "%50", "%51", "%52", "%53", "%54", "%55", "%56", "%57", "%58", "%59", "%5A",
        "%61", "%62", "%63", "%64", "%65", "%66", "%67", "%68", "%69", "%6A", "%6B", "%6C", "%6D", "%6E", "%6F", "%70", "%71", "%72", "%73", "%74", "%75", "%76", "%77", "%78", "%79", "%7A",
        "%30", "%31", "%32", "%33", "%34", "%35", "%36", "%37", "%38", "%39", "%2D", "%5F", "%2E", "%7E"
    ];

    private static readonly Dictionary<string, string> _bakedPercentEncodings =
        _unreservedCharactersPercentEncoded.ToDictionary(
            x => x,
            x => Convert.ToChar(Convert.ToUInt32(x[1..], 16)).ToString());

    internal static string PreparePathForComparison(string value)
    {
        /*
            Octets in the URI and robots.txt paths outside the range of the ASCII coded character set, and those in the
            reserved range defined by [RFC3986], MUST be percent-encoded as defined by [RFC3986] prior to comparison.
        */
        var encodedPath = EncodeUrlPath(value);

        /*
            If a percent-encoded ASCII octet is encountered in the URI, it MUST be unencoded prior to comparison,
            unless it is a reserved character in the URI as defined by [RFC3986] or the character is outside the unreserved character range.
        */
        return DecodePercentEncodedUnreservedCharacters(encodedPath);
    }

    private static string EncodeUrlPath(string value)
    {
        var pathAndTheRest = value.Split('?', 2);
        var path = pathAndTheRest[0];

        var encodedUrlPathBuilder = new StringBuilder();

        for (int i = 0; i < path.Length; i++)
        {
            var character = path[i];

            // skip over chars already % encoded
            if (character == '%'
                && i < path.Length - 2
                && char.IsAsciiHexDigit(path[i + 1])
                && char.IsAsciiHexDigit(path[i + 2]))
            {
                encodedUrlPathBuilder.Append('%');
                // normalize % encoding casing
                encodedUrlPathBuilder.Append(char.ToUpperInvariant(path[i + 1]));
                encodedUrlPathBuilder.Append(char.ToUpperInvariant(path[i + 2]));
                i += 2;
                continue;
            }

            // if (character == '/' || _pChars.Value.Contains(character)) encodedUrlPathBuilder.Append(character);
            if (character == '/' || (char.IsAscii(character) && !_reservedChars.Contains(character))) encodedUrlPathBuilder.Append(character);
            else encodedUrlPathBuilder.Append(PercentEncode(character));
        }

        if (pathAndTheRest.Length == 1)
            return encodedUrlPathBuilder.ToString();

        // fragment can be discarded for path rule matching
        var query = pathAndTheRest[1].Split('#', 2)[0];
        encodedUrlPathBuilder.Append('?');

        for (int i = 0; i < query.Length; i++)
        {
            var character = query[i];

            // skip over chars already % encoded
            if (character == '%'
                && i < query.Length - 2
                && char.IsAsciiHexDigit(query[i + 1])
                && char.IsAsciiHexDigit(query[i + 2]))
            {
                encodedUrlPathBuilder.Append('%');
                encodedUrlPathBuilder.Append(query[i + 1]);
                encodedUrlPathBuilder.Append(query[i + 2]);
                i += 2;
                continue;
            }

            if (char.IsAscii(character) && !_reservedChars.Contains(character)) encodedUrlPathBuilder.Append(character);
            else encodedUrlPathBuilder.Append(PercentEncode(character));
        }

        return encodedUrlPathBuilder.ToString();
    }

    private static string PercentEncode(char value)
    {
        var hexString = Convert.ToHexString(Encoding.UTF8.GetBytes(value.ToString()));
        return string.Create(hexString.Length / 2 * 3, hexString, (chars, state) =>
        {
            var hexStringSpan = state.AsSpan();
            for (var offset = 0; offset < chars.Length; offset += 3)
            {
                chars[offset] = '%';
                hexStringSpan.Slice(offset / 3 * 2, 2).CopyTo(chars.Slice(offset + 1, 2));
            }
        });
    }

    private static string DecodePercentEncodedUnreservedCharacters(string value)
    {
        foreach (var percentEncoding in _unreservedCharactersPercentEncoded)
        {
            value = value.Replace(
                percentEncoding,
                _bakedPercentEncodings[percentEncoding],
                StringComparison.InvariantCultureIgnoreCase);
        }

        return value;
    }
}
