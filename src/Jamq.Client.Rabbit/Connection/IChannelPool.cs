using Jamq.Client.Rabbit.Connection.Adapters;

namespace Jamq.Client.Rabbit.Connection;

internal interface IChannelPool : IDisposable
{
    IChannelAdapter Get();
}

internal interface IProducerChannelPool : IChannelPool
{
}

internal interface IConsumerChannelPool : IChannelPool
{
}