namespace RMQ.Client.Abstractions.Producing;

/// <summary>
/// Producer pipeline context
/// </summary>
public abstract class ProducerContext
{
    public string RoutingKey { get; }
    public object Message { get; }
    public IServiceProvider ServiceProvider { get; }

    public IDictionary<string, object> StoredValues { get; internal init; } = new Dictionary<string, object>();
    
    public byte[]? Body { get; set; }

    protected ProducerContext(
        string routingKey,
        object message,
        IServiceProvider serviceProvider)
    {
        RoutingKey = routingKey;
        Message = message;
        ServiceProvider = serviceProvider;
    }
}

public class ProducerContext<TNativeProperties> : ProducerContext
{
    internal ProducerContext(
        string routingKey,
        object message,
        IServiceProvider serviceProvider,
        TNativeProperties nativeProperties)
        : base(routingKey, message, serviceProvider)
    {
        NativeProperties = nativeProperties;
    }

    public TNativeProperties NativeProperties { get; }
}