using RabbitMQ.Client;

namespace RMQ.Client.Abstractions.Producing;

/// <summary>
/// Producer pipeline context
/// </summary>
public class ProducerContext
{
    public IBasicProperties NativeProperties { get; }
    public string RoutingKey { get; }
    public object Message { get; }
    public IServiceProvider ServiceProvider { get; }

    public IDictionary<string, object> StoredValues { get; } = new Dictionary<string, object>();
    
    public byte[]? Body { get; set; }

    public ProducerContext(
        IBasicProperties nativeProperties,
        string routingKey,
        object message,
        IServiceProvider serviceProvider)
    {
        NativeProperties = nativeProperties;
        RoutingKey = routingKey;
        Message = message;
        ServiceProvider = serviceProvider;
    }
}