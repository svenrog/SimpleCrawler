using SimpleCrawler.Console.Serialization;
using SimpleCrawler.Core.Checkpoints;
using System.Text.Json;

namespace SimpleCrawler.Console.Checkpoints;

public sealed class JsonFileCheckpointStore : ICheckpointStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _writeLock;

    public JsonFileCheckpointStore(string path)
    {
        _path = path;
        _writeLock = new SemaphoreSlim(1, 1);
    }

    public string Target => _path;

    public async ValueTask<CrawlState?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
            return null;

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync(stream, CrawlerJsonContext.Default.CrawlState, cancellationToken);
    }

    public async ValueTask SaveAsync(CrawlState state, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var temp = _path + ".tmp";

            await using (var stream = File.Create(temp))
                await JsonSerializer.SerializeAsync(stream, state, CrawlerJsonContext.Default.CrawlState, cancellationToken);

            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
