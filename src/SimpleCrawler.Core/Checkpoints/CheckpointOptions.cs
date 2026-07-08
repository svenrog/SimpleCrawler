namespace SimpleCrawler.Core.Checkpoints;

public sealed class CheckpointOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(15);
}
