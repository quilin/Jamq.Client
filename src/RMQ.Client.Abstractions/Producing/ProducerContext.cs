namespace RMQ.Client.Abstractions.Producing;

/// <summary>
/// Producer pipeline context
/// </summary>
public abstract class ProducerContext
{
    protected ProducerContext(
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }

    public IDictionary<string, object> StoredValues { get; internal init; } = new Dictionary<string, object>();
}

public abstract class ProducerContext<TNativeProperties> : ProducerContext
{
    protected ProducerContext(
        IServiceProvider serviceProvider,
        TNativeProperties nativeProperties)
        : base(serviceProvider)
    {
        NativeProperties = nativeProperties;
    }

    public TNativeProperties NativeProperties { get; }
}

public sealed class ProducerContext<TKey, TMessage, TNativeProperties> : ProducerContext<TNativeProperties>
{
    internal ProducerContext(
        IServiceProvider serviceProvider,
        TNativeProperties nativeProperties,
        TKey key,
        TMessage message) : base(serviceProvider, nativeProperties)
    {
        Key = key;
        Message = message;
    }

    public TKey Key { get; }

    public TMessage Message { get; }
}