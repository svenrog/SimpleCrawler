namespace SimpleCrawler.Js.Models;

/// <summary>
/// A <c>&lt;script&gt;</c> or <c>&lt;link&gt;</c> the page appended at runtime, as the DOM's resource queue
/// reports it (<c>__crawlerTakeResources</c>). Id is the queue's own handle, echoed back when the node's
/// load or error event is fired; Src is the attribute as authored, so it resolves against the document base
/// rather than the page; Type is the script's <c>type</c> attribute, which decides whether the source runs
/// as a module or as a classic script. Text is the node's own source, non-empty only when Src is empty:
/// an appended inline script has no URL to fetch and runs the text it carries.
/// </summary>
internal readonly record struct PendingResource(int Id, string? Tag, string? Src, string? Type, string? Text);
