using RabbitMQ.Client.Events;

namespace Jamq.Client.Rabbit.Consuming;

public class RabbitConsumerProperties
{
    internal RabbitConsumerProperties(
        BasicDeliverEventArgs basicDeliverEventArgs,
        RabbitConsumerParameters consumerParameters)
    {
        BasicDeliverEventArgs = basicDeliverEventArgs;
        ConsumerParameters = consumerParameters;
    }

    public BasicDeliverEventArgs BasicDeliverEventArgs { get; }
    public RabbitConsumerParameters ConsumerParameters { get; }
}