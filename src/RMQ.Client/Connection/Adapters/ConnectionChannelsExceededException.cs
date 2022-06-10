namespace RMQ.Client.Connection.Adapters;

internal class ConnectionChannelsExceededException : Exception
{
    public ConnectionChannelsExceededException(int maxChannelsCount)
        : base($"Connection can only support {maxChannelsCount} channels")
    {
    }
}