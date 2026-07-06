namespace Crawler.Tests.Common.Extensions;

internal static class AsyncDisposableExtensions
{
    public static void DisposeSync(this IAsyncDisposable disposable)
    {
        disposable.DisposeAsync().AwaitSync();
    }
}
