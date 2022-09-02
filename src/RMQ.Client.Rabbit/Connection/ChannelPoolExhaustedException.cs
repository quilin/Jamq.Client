namespace RMQ.Client.Rabbit.Connection;

internal class ChannelPoolExhaustedException : Exception
{
    public ChannelPoolExhaustedException(int poolSize)
        : base($"Channel pool is exhausted, only {poolSize} connections allowed per pool")
    {
    }
}