using System.Text.Json.Serialization;

namespace Crawler.Tests.Models;

public class LinkModel
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("href")]
    public required string Href { get; set; }
}
