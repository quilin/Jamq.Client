using RabbitMQ.Client;

namespace RMQ.Client.Rabbit.Connection.Adapters;

internal interface IChannelAdapter : IDisposable
{
    IModel Channel { get; }
    event EventHandler<ChannelDisruptedEventArgs> OnDisrupted;
}