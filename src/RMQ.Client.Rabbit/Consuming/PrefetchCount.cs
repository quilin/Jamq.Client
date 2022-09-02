namespace RMQ.Client.Rabbit.Consuming;

/// <summary>
/// RabbitMQ queue parameter, that states how many messages can broker send to consumers before getting the nack/ack
/// </summary>
public sealed class PrefetchCount
{
    internal PrefetchCount(ushort prefetchCount)
    {
        Value = prefetchCount;
    }

    /// <summary>
    /// Prefetch count value
    /// </summary>
    public ushort Value { get; }
}