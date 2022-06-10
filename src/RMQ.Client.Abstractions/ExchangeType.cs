namespace RMQ.Client.Abstractions;

/// <summary>
/// RabbitMQ exchange type
/// </summary>
public enum ExchangeType
{
    /// <summary>
    /// Topic exchange
    /// </summary>
    Topic = 0,

    /// <summary>
    /// Direct exchange
    /// </summary>
    Direct = 1,

    /// <summary>
    /// Fanout exchange
    /// </summary>
    Fanout = 2,

    /// <summary>
    /// Headers exchange
    /// </summary>
    Headers = 3
}