using System.Text;

namespace Crawler.AngleSharp.Js.V8;

internal sealed class V8StackTraceFormatter
{
    private readonly int _sourceContextRadius;

    public V8StackTraceFormatter(int sourceContextRadius)
    {
        _sourceContextRadius = sourceContextRadius;
    }

    // ClearScript's ErrorDetails leads with the error message (logged separately, so dropped here) and then
    // appends each stack frame's full source line after " -> "; for a minified bundle that source is an entire
    // file per frame, which buries the actual call chain. Keep the frame locations but replace the full source
    // dump with a short window centred on the frame's column, leaving a trace shaped like Jint's
    // JavaScriptStackTrace plus a glimpse of the offending code.
    public string? Format(string message, string? details)
    {
        if (string.IsNullOrEmpty(details))
            return details;

        var builder = new StringBuilder(details.Length);
        var isFirstLine = true;
        foreach (var line in details.AsSpan().EnumerateLines())
        {
            if (isFirstLine)
            {
                isFirstLine = false;
                if (line.Trim().SequenceEqual(message.AsSpan().Trim()))
                    continue;
            }

            if (builder.Length > 0)
                builder.Append('\n');

            var arrow = line.IndexOf(" -> ");
            if (arrow >= 0 && line.TrimStart().StartsWith("at "))
            {
                var location = line[..arrow];
                builder.Append(location.TrimEnd());
                AppendSourceSnippet(builder, location, line[(arrow + 4)..]);
            }
            else
            {
                builder.Append(line);
            }
        }

        return builder.ToString();
    }

    private void AppendSourceSnippet(StringBuilder builder, ReadOnlySpan<char> location, ReadOnlySpan<char> source)
    {
        if (source.IsEmpty)
            return;

        var column = ParseColumn(location);
        var center = column > 0 ? Math.Min(column - 1, source.Length) : 0;
        var start = Math.Max(0, center - _sourceContextRadius);
        var end = Math.Min(source.Length, center + _sourceContextRadius);

        builder.Append(" -> ");
        if (start > 0)
            builder.Append('…');
        builder.Append(source[start..end]);
        if (end < source.Length)
            builder.Append('…');
    }

    // A frame location ends with ":line:column", optionally wrapped in parentheses (e.g. "at f (Script [3]:2:88)").
    private static int ParseColumn(ReadOnlySpan<char> location)
    {
        var loc = location.TrimEnd();
        if (loc.EndsWith(")"))
            loc = loc[..^1];

        var lastColon = loc.LastIndexOf(':');
        if (lastColon < 0)
            return -1;

        return int.TryParse(loc[(lastColon + 1)..], out var column) ? column : -1;
    }
}
