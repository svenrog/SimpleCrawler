using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Crawler.AngleSharp.Js;

internal static partial class EsModuleLoader
{
    public static async Task<string?> BuildAsync(IReadOnlyList<ModuleSource> entries, HttpClient client, CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
            return null;

        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var queue = new Queue<ModuleSource>(entries);

        while (queue.Count > 0)
        {
            var module = queue.Dequeue();
            if (sources.ContainsKey(module.Key))
                continue;

            var rewritten = Rewrite(module.Source, module.BaseUri, out var specifiers);
            sources[module.Key] = rewritten;

            foreach (var specifier in specifiers)
            {
                if (sources.ContainsKey(specifier))
                    continue;

                var source = await FetchAsync(client, specifier, cancellationToken);
                if (source == null)
                    continue;

                queue.Enqueue(new ModuleSource(specifier, source, new Uri(specifier)));
            }
        }

        var builder = new StringBuilder();
        foreach (var (key, source) in sources)
        {
            builder.Append("__modules.register(").Append(JsonSerializer.Serialize(key)).Append(", function (__exp, __require, __import) {\n");
            builder.Append(source);
            builder.Append("\n});\n");
        }

        foreach (var entry in entries)
            builder.Append("__modules.evaluate(").Append(JsonSerializer.Serialize(entry.Key)).Append(");\n");

        return builder.ToString();
    }

    private static async Task<string?> FetchAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string Rewrite(string source, Uri baseUri, out List<string> specifiers)
    {
        var found = new List<string>();

        string Resolve(string specifier)
        {
            var absolute = new Uri(baseUri, specifier).ToString();
            found.Add(absolute);
            return absolute;
        }

        // Static imports are hoisted to the top of every bundler-emitted module, so only the
        // leading run of import statements is real module syntax. Rewriting `import` globally
        // would corrupt string literals like stylis's "@import" that live deeper in the code.
        var prefix = new StringBuilder();
        var position = 0;
        while (position < source.Length)
        {
            var scan = position;
            while (scan < source.Length && (char.IsWhiteSpace(source[scan]) || source[scan] == ';'))
                scan++;

            var match = _leadingImport.Match(source, scan);
            if (!match.Success || match.Index != scan)
                break;

            prefix.Append(RewriteImport(match, Resolve)).Append('\n');
            position = match.Index + match.Length;
        }

        var rest = source[position..];
        rest = _dynamicImport.Replace(rest, m => $"__import({JsonSerializer.Serialize(Resolve(m.Groups["spec"].Value))})");
        rest = _exportNamed.Replace(rest, m => RewriteNamedExport(m.Groups["body"].Value));
        rest = _exportDefault.Replace(rest, "__exp.default = ");

        specifiers = found;
        return prefix.Append(rest).ToString();
    }

    private static string RewriteImport(Match match, Func<string, string> resolve)
    {
        if (match.Groups["sideSpec"].Success)
            return $"__require({JsonSerializer.Serialize(resolve(match.Groups["sideSpec"].Value))});";

        var key = JsonSerializer.Serialize(resolve(match.Groups["spec"].Value));
        var require = $"__require({key})";
        var clause = match.Groups["clause"].Value.Trim();

        if (clause.StartsWith('{'))
        {
            var bindings = SplitMembers(clause.Trim('{', '}'))
                .Select(member =>
                {
                    var (name, local) = SplitAlias(member);
                    return $"{name}: {local}";
                });

            return $"const {{ {string.Join(", ", bindings)} }} = {require};";
        }

        if (clause.StartsWith('*'))
        {
            var local = clause[1..].TrimStart()[2..].Trim();
            return $"const {local} = {require};";
        }

        var comma = clause.IndexOf(',');
        if (comma < 0)
            return $"const {clause} = {require}.default;";

        var defaultLocal = clause[..comma].Trim();
        var namedBindings = SplitMembers(clause[(comma + 1)..].Trim().Trim('{', '}'))
            .Select(member =>
            {
                var (name, local) = SplitAlias(member);
                return $"{name}: {local}";
            });

        return $"const {defaultLocal} = {require}.default; const {{ {string.Join(", ", namedBindings)} }} = {require};";
    }

    private static string RewriteNamedExport(string body)
    {
        var assignments = SplitMembers(body).Select(member =>
        {
            var (local, exported) = SplitAlias(member);
            return $"__exp.{exported} = {local};";
        });

        return string.Join(" ", assignments);
    }

    private static IEnumerable<string> SplitMembers(string body)
    {
        return body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static (string, string) SplitAlias(string member)
    {
        var parts = member.Split(" as ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], parts[0]);
    }

    [GeneratedRegex("""(?<![\w$.])import\s*\(\s*(["'])(?<spec>[^"']+)\1\s*\)""")]
    private static partial Regex DynamicImportRegex();

    [GeneratedRegex("""\Gimport\s*(?:(?<clause>\{[^}]*\}|\*\s*as\s+[\w$]+|[\w$]+\s*,\s*\{[^}]*\}|[\w$]+)\s*from\s*(?<q>["'])(?<spec>[^"']+)\k<q>|(?<sq>["'])(?<sideSpec>[^"']+)\k<sq>)\s*;?""")]
    private static partial Regex LeadingImportRegex();

    [GeneratedRegex("""(?<![\w$.])export\s*\{(?<body>[\s\w$,]*)\}\s*;?""")]
    private static partial Regex NamedExportRegex();

    [GeneratedRegex("""(?<![\w$.])export\s+default\s+""")]
    private static partial Regex ExportDefaultRegex();

    private static readonly Regex _dynamicImport = DynamicImportRegex();
    private static readonly Regex _leadingImport = LeadingImportRegex();
    private static readonly Regex _exportNamed = NamedExportRegex();
    private static readonly Regex _exportDefault = ExportDefaultRegex();
}
