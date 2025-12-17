using Jamq.Client.Rabbit.Connection.Adapters;

namespace Jamq.Client.Rabbit.Connection;

internal interface IChannelProvider : IDisposable
{
    IChannelAdapter Get();
}

internal interface IProducerChannelProvider : IChannelProvider
{
}

internal interface IConsumerChannelProvider : IChannelProvider
{
}