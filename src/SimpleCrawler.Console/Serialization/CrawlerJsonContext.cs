using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Models;
using System.Text.Json.Serialization;

namespace SimpleCrawler.Console.Serialization;

/// <summary>
/// Single source-generated context for everything the CLI (de)serializes. CrawlState already pulls in
/// UrlReport for checkpoints, so the --report output serializes the same type through the same context
/// rather than a parallel DTO. Outcomes are written as names for readability.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(CrawlState))]
[JsonSerializable(typeof(IReadOnlyCollection<UrlReport>))]
internal sealed partial class CrawlerJsonContext : JsonSerializerContext
{
}
