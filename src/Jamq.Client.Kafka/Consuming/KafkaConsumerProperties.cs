using Confluent.Kafka;

namespace Jamq.Client.Kafka.Consuming;

public class KafkaConsumerProperties<TKey, TMessage>
{
    public KafkaConsumerProperties(
        ConsumeResult<TKey, TMessage> consumeResult,
        KafkaConsumerParameters parameters)
    {
        ConsumeResult = consumeResult;
        Parameters = parameters;
    }

    public ConsumeResult<TKey, TMessage> ConsumeResult { get; }
    public KafkaConsumerParameters Parameters { get; }
}