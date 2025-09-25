using System.Collections.Concurrent;

namespace SimpleCrawler.Extensions;

public static class QueueExtensions
{
    public static void EnqueueAll<T>(this ConcurrentQueue<T> queue, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            queue.Enqueue(item);
        }
    }
}
