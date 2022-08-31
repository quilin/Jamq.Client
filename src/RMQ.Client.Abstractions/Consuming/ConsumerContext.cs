namespace RMQ.Client.Abstractions.Consuming;

/// <summary>
/// Incoming message pipeline context
/// Client and message agnostic
/// <remarks>Can be useful for cases like metrics and logging where the content of the message and details of the consumer client are irrelevant</remarks>
/// </summary>
public abstract class ConsumerContext
{
    protected ConsumerContext(
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// Service provider
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Values to share between the pipeline steps
    /// </summary>
    public IDictionary<string, object> StoredValues { get; internal init; } = new Dictionary<string, object>();
}

/// <summary>
/// Incoming message pipeline context
/// Message agnostic
/// <remarks>Can be used for cases like CorrelationToken passing, where the content of the message is irrelevant</remarks>
/// </summary>
public abstract class ConsumerContext<TNativeProperties> : ConsumerContext
{
    protected ConsumerContext(
        IServiceProvider serviceProvider,
        TNativeProperties nativeProperties)
        : base(serviceProvider)
    {
        NativeProperties = nativeProperties;
    }

    /// <summary>
    /// Native RabbitMQ library incoming message event
    /// </summary>
    public TNativeProperties NativeProperties { get; }
}

/// <summary>
/// Incoming message pipeline context
/// </summary>
public sealed class ConsumerContext<TMessage, TNativeProperties> : ConsumerContext<TNativeProperties>
{
    internal ConsumerContext(IServiceProvider serviceProvider, TNativeProperties nativeProperties)
        : base(serviceProvider, nativeProperties)
    {
    }

    /// <summary>
    /// Decoded incoming message
    /// </summary>
    public TMessage? Message { get; set; }
}