namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsLocalStorage
{
    private readonly Dictionary<string, string> _store = new(StringComparer.Ordinal);

    public int length => _store.Count;

    public string? getItem(string key) => _store.TryGetValue(key, out var value) ? value : null;
    public void setItem(string key, object? value) => _store[key] = value?.ToString() ?? string.Empty;
    public void removeItem(string key) => _store.Remove(key);
    public void clear() => _store.Clear();
    public string? key(int index) => index >= 0 && index < _store.Count ? _store.Keys.ElementAt(index) : null;
}
