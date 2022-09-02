using Confluent.Kafka;

namespace Jamq.Client.Kafka.Producing;

/// <summary>
/// Kafka producer parameters
/// </summary>
public class KafkaProducerParameters
{
    public ProducerConfig ProducerConfig { get; }
    public string Topic { get; }

    public KafkaProducerParameters(
        ProducerConfig producerConfig,
        string topic)
    {
        ProducerConfig = producerConfig;
        Topic = topic;
    }
}