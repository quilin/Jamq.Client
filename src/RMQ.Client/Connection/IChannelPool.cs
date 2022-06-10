using RMQ.Client.Connection.Adapters;

namespace RMQ.Client.Connection;

internal interface IChannelPool : IDisposable
{
    IChannelAdapter Get();
}

internal interface IProducerChannelPool : IChannelPool
{
}