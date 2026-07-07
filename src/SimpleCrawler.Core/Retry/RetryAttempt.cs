namespace SimpleCrawler.Core.Retry;

public readonly struct RetryAttempt<T>
{
    private RetryAttempt(T value, RetryReason? failure)
    {
        Value = value;
        Failure = failure;
    }

    public T Value { get; }

    public RetryReason? Failure { get; }

    public bool Succeeded => Failure is null;

    public static RetryAttempt<T> Ok(T value) => new(value, null);

    public static RetryAttempt<T> Failed(RetryReason reason, T value = default!) => new(value, reason);
}
