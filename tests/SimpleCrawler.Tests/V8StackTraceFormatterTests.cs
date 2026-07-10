using SimpleCrawler.Js.V8;

namespace SimpleCrawler.Tests;

/// <summary>
/// A minified bundle can carry literal newlines inside a single source line (e.g. core-js' whitespace
/// feature-detect strings), which split one stack frame's source across several physical lines. The frame's
/// source must still be windowed as a whole rather than dumping every continuation line verbatim.
/// </summary>
public class V8StackTraceFormatterTests
{
    private const int _radius = 48;

    [Fact]
    public void Format_WindowsSourceSpanningEmbeddedNewlines()
    {
        var formatter = new V8StackTraceFormatter(_radius);
        var message = "TypeError: Cannot read properties of null (reading split)";

        var head = new string('h', 100);
        var lead = new string('f', 400);
        var trail = new string('g', 400);
        var marker = "SPLIT_HERE";
        var source = head + "\n" + lead + marker + trail;
        var column = head.Length + 1 + lead.Length + 1;
        var details = $"{message}\n    at Script [543]:2:{column} -> {source}";

        var result = formatter.Format(message, details)!;

        Assert.Contains("at Script [543]:2:" + column, result);
        Assert.Contains(marker, result);
        Assert.Contains('…', result);
        // The frame's source is windowed, so the far ends never reach the output verbatim.
        Assert.DoesNotContain(head, result);
        Assert.DoesNotContain(lead, result);
        Assert.DoesNotContain(trail, result);
        Assert.True(result.Length < details.Length / 4, $"expected a truncated trace, got {result.Length} chars");
    }

    [Fact]
    public void Format_LeavesSingleLineFramesIntact()
    {
        var formatter = new V8StackTraceFormatter(_radius);
        var message = "TypeError: boom";
        var source = new string('x', 500);
        var details = $"{message}\n    at f (Script [3]:2:88) -> {source}";

        var result = formatter.Format(message, details)!;

        Assert.Contains("at f (Script [3]:2:88)", result);
        Assert.Contains('…', result);
        Assert.True(result.Length < details.Length, "long source should be windowed");
        Assert.DoesNotContain(message, result);
    }
}
