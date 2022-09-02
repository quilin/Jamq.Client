using RabbitMQ.Client.Events;
using RMQ.Client.Rabbit.Connection.Adapters;

namespace RMQ.Client.Rabbit.Consuming;

internal class ConsumerConnectionState
{
    public AsyncEventHandler<BasicDeliverEventArgs> IncomingMessageHandler { get; }
    public AsyncEventingBasicConsumer Consumer { get; }
    public IChannelAdapter ChannelAdapter { get; }
    public string ConsumerTag { get; }

    public ConsumerConnectionState(
        AsyncEventHandler<BasicDeliverEventArgs> incomingMessageHandler,
        AsyncEventingBasicConsumer consumer,
        IChannelAdapter channelAdapter,
        string consumerTag)
    {
        IncomingMessageHandler = incomingMessageHandler;
        Consumer = consumer;
        ChannelAdapter = channelAdapter;
        ConsumerTag = consumerTag;
    }
}