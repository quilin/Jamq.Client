namespace RMQ.Client.Abstractions;

/// <summary>
/// Parameters for creating and operating the RabbitMQ producers
/// </summary>
public class RabbitProducerParameters
{
    /// <summary>
    /// Create default topic producer parameters for given exchange name
    /// </summary>
    /// <param name="exchangeName"></param>
    public RabbitProducerParameters(string exchangeName)
    {
        ExchangeName = exchangeName;
    }

    /// <summary>
    /// Exchange name
    /// </summary>
    public string ExchangeName { get; }

    /// <summary>
    /// Exchange type
    /// </summary>
    public ExchangeType ExchangeType { get; init; }

    /// <summary>
    /// Publishing timeout
    /// <remarks>If set to null, the client will not await for broker ack</remarks>
    /// </summary>
    public TimeSpan? PublishingTimeout { get; init; }
}