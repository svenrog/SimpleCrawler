using Acornima.Ast;
using System.Runtime.CompilerServices;

namespace SimpleCrawler.Js.Jint;

// Jint's Function.prototype.toString() returns a "[native code]" stub for every ordinary script
// function unless Options.Host.FunctionToStringHandler supplies real text, and that handler is only
// given the AST node — not the source it came from. Node.UserData looks like the obvious place to
// stash it, but Jint uses that slot itself for internal block-scope caching (BlockState); writing to
// it corrupts unrelated script execution. A side table keyed by node reference avoids touching Jint's
// own state. Each prepared script/module is tagged once at parse time so the handler can slice
// node.Start..node.End straight out of its source; the walk is iterative because minified bundles nest
// deep enough to risk a CLR stack overflow with recursion.
internal static class JintFunctionSourceTagger
{
    private static readonly ConditionalWeakTable<Node, string> _sources = [];

    public static void Tag(Node? root, string source)
    {
        if (root is null)
            return;

        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            _sources.AddOrUpdate(node, source);

            foreach (var child in node.ChildNodes)
            {
                if (child is not null)
                    stack.Push(child);
            }
        }
    }

    public static string? TryGetSlice(Node node)
    {
        if (!_sources.TryGetValue(node, out var source) || node.Start < 0 || node.End < node.Start || node.End > source.Length)
            return null;

        return source.Substring(node.Start, node.End - node.Start);
    }
}
