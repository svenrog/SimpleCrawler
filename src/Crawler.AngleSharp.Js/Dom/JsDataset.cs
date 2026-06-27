using AngleSharp.Dom;
using System.Dynamic;
using System.Text;

namespace Crawler.AngleSharp.Js.Dom;

// element.dataset: a live view of the element's data-* attributes. A DynamicObject (like JsStyle) because
// the key set is open — bundles read dataset.language, dataset.theme, etc. ClearScript/V8's get path only
// surfaces members reported by GetDynamicMemberNames, so those must be enumerated for reads to resolve
// (the same gotcha the expando wrappers hit); Jint routes straight through TryGetMember.
public sealed class JsDataset : DynamicObject
{
    private const string _prefix = "data-";

    private readonly IElement _element;

    internal JsDataset(IElement element)
    {
        _element = element;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = _element.GetAttribute(_prefix + CamelToKebab(binder.Name));
        return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _element.SetAttribute(_prefix + CamelToKebab(binder.Name), value?.ToString() ?? string.Empty);
        return true;
    }

    public override IEnumerable<string> GetDynamicMemberNames()
    {
        foreach (var attribute in _element.Attributes)
        {
            if (attribute.Name.StartsWith(_prefix, StringComparison.Ordinal))
                yield return KebabToCamel(attribute.Name[_prefix.Length..]);
        }
    }

    private static string CamelToKebab(string name)
    {
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

    private static string KebabToCamel(string name)
    {
        var builder = new StringBuilder(name.Length);
        var upperNext = false;
        foreach (var character in name)
        {
            if (character == '-')
            {
                upperNext = true;
            }
            else if (upperNext)
            {
                builder.Append(char.ToUpperInvariant(character));
                upperNext = false;
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
