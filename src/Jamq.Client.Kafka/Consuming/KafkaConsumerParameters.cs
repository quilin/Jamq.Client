using Confluent.Kafka;

namespace Jamq.Client.Kafka.Consuming;

/// <summary>
/// Kafka consumer parameters
/// </summary>
public class KafkaConsumerParameters
{
    public KafkaConsumerParameters(
        ConsumerConfig consumerConfig,
        string topic)
    {
        ConsumerConfig = consumerConfig;
        Topic = topic;
    }

    /// <summary>
    /// Native Confluent.Kafka consumer configuration
    /// </summary>
    public ConsumerConfig ConsumerConfig { get; }

    /// <summary>
    /// Topic name
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// Consume from broker timeout
    /// </summary>
    public TimeSpan ConsumeTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// If broker responds with an empty message or EoF, wait for this amount of time
    /// </summary>
    public TimeSpan IdleInterval { get; init; } = TimeSpan.FromSeconds(1);
}