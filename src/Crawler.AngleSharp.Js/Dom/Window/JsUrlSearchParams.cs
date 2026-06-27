using System.Collections;
using System.Text;

namespace Crawler.AngleSharp.Js.Dom.Window;

// A live URLSearchParams: the mutating methods (set/append/delete) are what SPAs and SDKs use to build
// request URLs — e.g. `new URL(path, base).searchParams.set(...)` then `fetch(url)` — so the parent URL's
// search/href must reflect these writes. It also iterates as [key, value] pairs so Object.fromEntries works.
public sealed class JsUrlSearchParams : IEnumerable<object?>
{
    private readonly List<string[]> _pairs = [];

    public JsUrlSearchParams(string query)
    {
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        if (trimmed.Length == 0)
            return;

        foreach (var part in trimmed.Split('&'))
        {
            if (part.Length == 0)
                continue;

            var index = part.IndexOf('=');
            var key = index < 0 ? part : part[..index];
            var value = index < 0 ? string.Empty : part[(index + 1)..];
            _pairs.Add([Uri.UnescapeDataString(key), Uri.UnescapeDataString(value)]);
        }
    }

    public object? get(string name)
    {
        foreach (var pair in _pairs)
            if (pair[0] == name)
                return pair[1];

        return null;
    }

    public List<object?> getAll(string name)
    {
        var result = new List<object?>();
        foreach (var pair in _pairs)
            if (pair[0] == name)
                result.Add(pair[1]);

        return result;
    }

    public bool has(string name)
    {
        foreach (var pair in _pairs)
            if (pair[0] == name)
                return true;

        return false;
    }

    public void set(string name, object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        var found = false;
        for (var i = 0; i < _pairs.Count;)
        {
            if (_pairs[i][0] != name)
            {
                i++;
            }
            else if (!found)
            {
                _pairs[i][1] = text;
                found = true;
                i++;
            }
            else
            {
                _pairs.RemoveAt(i);
            }
        }

        if (!found)
            _pairs.Add([name, text]);
    }

    public void append(string name, object? value)
    {
        _pairs.Add([name, value?.ToString() ?? string.Empty]);
    }

    public void delete(string name)
    {
        _pairs.RemoveAll(pair => pair[0] == name);
    }

    public List<object?> keys()
    {
        var result = new List<object?>();
        foreach (var pair in _pairs)
            result.Add(pair[0]);

        return result;
    }

    public void sort()
    {
        _pairs.Sort(static (a, b) => string.CompareOrdinal(a[0], b[0]));
    }

    public override string ToString()
    {
        if (_pairs.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var pair in _pairs)
        {
            if (builder.Length > 0)
                builder.Append('&');

            builder.Append(Uri.EscapeDataString(pair[0]));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(pair[1]));
        }

        return builder.ToString();
    }

    public IEnumerator<object?> GetEnumerator()
    {
        foreach (var pair in _pairs)
            yield return new object?[] { pair[0], pair[1] };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
