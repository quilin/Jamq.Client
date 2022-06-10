namespace RMQ.Client.Connection.Adapters;

internal interface IConnectionAdapter : IDisposable
{
    Guid Id { get; }
    decimal BusinessRatio { get; }
    ConnectionBusinessStatus Status { get; }

    IChannelAdapter OpenChannel();
    event EventHandler<ConnectionDisruptedEventArgs> OnDisrupted;
}