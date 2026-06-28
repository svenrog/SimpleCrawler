namespace Crawler.AngleSharp.Js.Dom.Helpers;

internal static class JsValue
{
    // JavaScript truthiness: false, 0, -0, NaN, "" and null/undefined are falsy; everything else is truthy.
    // The engines hand us host values as bool/string/numeric boxes, so coerce numbers through IConvertible.
    public static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => s.Length > 0,
        IConvertible c => c.ToDouble(null) is var d && d != 0 && !double.IsNaN(d),
        _ => true
    };
}
