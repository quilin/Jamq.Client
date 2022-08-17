namespace RMQ.Client.Abstractions.Consuming;

/// <summary>
/// Incoming message pipeline context
/// </summary>
public abstract class ConsumerContext
{
    /// <summary>
    /// Service provider
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Values to share between the pipeline steps
    /// </summary>
    public IDictionary<string, object> StoredValues { get; internal init; } = new Dictionary<string, object>();

    protected ConsumerContext(
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }
}

public class ConsumerContext<TNativeProperties, TMessage> : ConsumerContext
{
    internal ConsumerContext(IServiceProvider serviceProvider, TNativeProperties nativeProperties)
        : base(serviceProvider)
    {
        NativeProperties = nativeProperties;
    }

    /// <summary>
    /// Native RabbitMQ library incoming message event
    /// </summary>
    public TNativeProperties NativeProperties { get; }

    /// <summary>
    /// Decoded incoming message
    /// </summary>
    public TMessage? Message { get; set; }
}