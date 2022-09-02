using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.Connection.Adapters;

internal interface IChannelAdapter : IDisposable
{
    IModel Channel { get; }
    event EventHandler<ChannelDisruptedEventArgs> OnDisrupted;
}