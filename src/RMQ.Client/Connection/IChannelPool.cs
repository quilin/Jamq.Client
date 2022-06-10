using RMQ.Client.Connection.Adapters;

namespace RMQ.Client.Connection;

internal interface IChannelPool : IDisposable
{
    Task<IChannelAdapter> Get();
}

internal interface IProducerChannelPool : IChannelPool
{
}