using AngleSharp.Dom;
using System.Dynamic;
using System.Text;

namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsStyle : DynamicObject
{
    private readonly IElement _element;
    private readonly List<KeyValuePair<string, string>> _declarations = [];

    internal JsStyle(IElement element)
    {
        _element = element;
        ParseInto(element.GetAttribute("style"));
    }

    public string cssText
    {
        get => Serialize();
        set => SetCssText(value);
    }

    public void setProperty(string name, object? value, object? priority = null) => Set(name, value?.ToString() ?? string.Empty);

    public void removeProperty(string name)
    {
        _declarations.RemoveAll(declaration => string.Equals(declaration.Key, name, StringComparison.Ordinal));
        WriteBack();
    }

    public string getPropertyValue(string name)
    {
        foreach (var declaration in _declarations)
        {
            if (string.Equals(declaration.Key, name, StringComparison.Ordinal))
                return declaration.Value;
        }

        return string.Empty;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        if (string.Equals(binder.Name, "cssText", StringComparison.Ordinal))
            SetCssText(value?.ToString());
        else
            Set(CamelToKebab(binder.Name), value?.ToString() ?? string.Empty);

        return true;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = null;
        return false;
    }

    private void SetCssText(string? value)
    {
        _declarations.Clear();
        ParseInto(value);
        WriteBack();
    }

    private void Set(string name, string value)
    {
        if (string.IsNullOrEmpty(name))
            return;

        var index = _declarations.FindIndex(declaration => string.Equals(declaration.Key, name, StringComparison.Ordinal));
        var pair = new KeyValuePair<string, string>(name, value);
        if (index >= 0)
            _declarations[index] = pair;
        else
            _declarations.Add(pair);

        WriteBack();
    }

    private void ParseInto(string? cssText)
    {
        if (string.IsNullOrWhiteSpace(cssText))
            return;

        foreach (var part in cssText.Split(';'))
        {
            var index = part.IndexOf(':');
            if (index <= 0)
                continue;

            var name = part[..index].Trim();
            var value = part[(index + 1)..].Trim();
            if (name.Length > 0)
                _declarations.Add(new KeyValuePair<string, string>(name, value));
        }
    }

    private string Serialize()
    {
        var builder = new StringBuilder();
        foreach (var declaration in _declarations)
        {
            if (builder.Length > 0)
                builder.Append(' ');

            builder.Append(declaration.Key).Append(": ").Append(declaration.Value).Append(';');
        }

        return builder.ToString();
    }

    private void WriteBack()
    {
        if (_declarations.Count == 0)
            _element.RemoveAttribute("style");
        else
            _element.SetAttribute("style", Serialize());
    }

    private static string CamelToKebab(string name)
    {
        if (name.StartsWith("--", StringComparison.Ordinal))
            return name;

        var builder = new StringBuilder(name.Length + 4);
        foreach (var character in name)
        {
            if (char.IsUpper(character))
                builder.Append('-').Append(char.ToLowerInvariant(character));
            else
                builder.Append(character);
        }

        return builder.ToString();
    }
}
