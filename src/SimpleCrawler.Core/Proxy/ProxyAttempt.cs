namespace SimpleCrawler.Core.Proxy;

public readonly struct ProxyAttempt<T>
{
    private ProxyAttempt(T value, ProxyFailureKind? failure)
    {
        Value = value;
        Failure = failure;
    }

    public T Value { get; }

    public ProxyFailureKind? Failure { get; }

    public bool Succeeded => Failure is null;

    public static ProxyAttempt<T> Ok(T value) => new(value, null);

    public static ProxyAttempt<T> Failed(ProxyFailureKind kind, T value = default!) => new(value, kind);
}
