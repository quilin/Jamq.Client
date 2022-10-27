using Confluent.Kafka;

namespace Jamq.Client.Kafka.Producing;

public class KafkaProducerProperties<TKey, TMessage>
{
    public KafkaProducerProperties(
        Message<TKey, TMessage> message,
        KafkaProducerParameters parameters)
    {
        Message = message;
        Parameters = parameters;
    }

    public Message<TKey, TMessage> Message { get; }
    public KafkaProducerParameters Parameters { get; }
}