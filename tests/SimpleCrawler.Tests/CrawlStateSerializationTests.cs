using SimpleCrawler.Core.Checkpoints;
using System.Text.Json;

namespace SimpleCrawler.Tests;

/// <summary>
/// Guards the checkpoint format: the full-corpus dedup set is not serialized, only the pending frontier
/// (with depth) is, and the dedup set is reconstructed from Processed plus the frontier on load.
/// </summary>
public class CrawlStateSerializationTests
{
    [Fact]
    public void Discovered_Is_Not_Serialized()
    {
        var state = new CrawlState
        {
            Entries = ["http://h/"],
            Processed = ["http://h/", "http://h/a"],
            Visited = ["http://h/"],
        };
        state.Frontier["http://h/b"] = 2;
        state.Discovered.UnionWith(["http://h/", "http://h/a", "http://h/b"]);

        var json = JsonSerializer.Serialize(state);

        Assert.DoesNotContain("\"Discovered\"", json);
        Assert.Contains("\"Frontier\"", json);
    }

    [Fact]
    public void Roundtrip_Preserves_Frontier_Depth_And_Rebuilds_Discovered()
    {
        var original = new CrawlState
        {
            Entries = ["http://h/"],
            Processed = ["http://h/", "http://h/a"],
            Visited = ["http://h/", "http://h/a"],
        };
        original.Frontier["http://h/b"] = 3;

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<CrawlState>(json)!;

        // Depth survives.
        Assert.Equal(3, restored.Frontier["http://h/b"]);

        // Discovered is empty until rebuilt, then equals Processed plus the pending frontier.
        Assert.Empty(restored.Discovered);
        restored.RebuildAfterLoad();

        Assert.Equal(
            ["http://h/", "http://h/a", "http://h/b"],
            restored.Discovered.OrderBy(u => u));
    }
}
