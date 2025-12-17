using Jamq.Client.Rabbit.Connection.Adapters;
using RabbitMQ.Client.Events;

namespace Jamq.Client.Rabbit.Consuming;

internal record ConsumerConnectionState(
    AsyncEventHandler<BasicDeliverEventArgs> IncomingMessageHandler,
    AsyncEventingBasicConsumer Consumer,
    IChannelAdapter ChannelAdapter,
    string ConsumerTag);