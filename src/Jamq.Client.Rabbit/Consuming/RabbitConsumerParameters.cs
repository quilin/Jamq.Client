namespace Jamq.Client.Rabbit.Consuming;

/// <summary>
/// Parameters for creating the RabbitMQ consumer
/// </summary>
public class RabbitConsumerParameters
{
    /// <summary>
    /// Consumer tag, human-readable
    /// <remarks>Will have unique suffix</remarks>
    /// </summary>
    public string ConsumerTag { get; }

    /// <summary>
    /// Processing order
    /// Initialize via <see cref="ProcessingOrder" />
    /// </summary>
    public PrefetchCount ProcessingOrder { get; }

    /// <summary>
    /// Queue name
    /// <remarks>Exclusive queues will have unique suffix</remarks>
    /// </summary>
    public string QueueName { get; }

    /// <summary>
    /// Bind exchange name
    /// </summary>
    public string? ExchangeName { get; init; }

    /// <summary>
    /// Bind exchange type
    /// </summary>
    public ExchangeType ExchangeType { get; init; }

    /// <summary>
    /// Declared queue name for internal usage
    /// </summary>
    internal string? DeclaredQueueName { get; set; }

    /// <summary>
    /// Binding keys for queue-to-exchange binding
    /// </summary>
    public IEnumerable<string>? RoutingKeys { get; init; }

    /// <summary>
    /// Queue exclusiveness flag
    /// </summary>
    public bool Exclusive { get; init; }

    /// <summary>
    /// DLX name
    /// </summary>
    public string? DeadLetterExchange { get; init; }

    /// <summary>
    /// Maximum amount of time the consumer is allowed to wait for message processing to end when subscription is cancelled
    /// </summary>
    public TimeSpan MaxProcessingAnticipation { get; init; } = TimeSpan.FromSeconds(30);

    public IDictionary<string, object>? AdditionalQueueArguments { get; init; }

    public IDictionary<string, object>? AdditionalExchangeArguments { get; init; }

    public RabbitConsumerParameters(
        string consumerTag,
        string queueName,
        PrefetchCount processingOrder)
    {
        ConsumerTag = consumerTag;
        QueueName = queueName;
        ProcessingOrder = processingOrder;
    }
}