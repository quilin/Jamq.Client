using RabbitMQ.Client.Events;

namespace Jamq.Client.Rabbit.Consuming;

public class RabbitConsumerProperties
{
    internal RabbitConsumerProperties(
        BasicDeliverEventArgs basicDeliverEventArgs,
        RabbitConsumerParameters parameters)
    {
        BasicDeliverEventArgs = basicDeliverEventArgs;
        Parameters = parameters;
    }

    public BasicDeliverEventArgs BasicDeliverEventArgs { get; }

    public RabbitConsumerParameters Parameters { get; }
}