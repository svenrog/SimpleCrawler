namespace Crawler.Tests.Common.Extensions;

internal static class TaskExtensions
{
    public static void AwaitSync(this ValueTask valueTask)
    {
        if (valueTask.IsCompleted)
            return;

        AwaitSync(valueTask.AsTask());
    }

    public static void AwaitSync(this Task task)
    {
        task.GetAwaiter().GetResult();
    }
}
