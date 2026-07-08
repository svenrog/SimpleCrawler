using SimpleCrawler.Core.Checkpoints;
using System.Text.Json.Serialization;

namespace SimpleCrawler.Console.Checkpoints;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(CrawlState))]
internal sealed partial class CheckpointJsonContext : JsonSerializerContext
{
}
