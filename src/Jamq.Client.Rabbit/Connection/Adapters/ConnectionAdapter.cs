using Jamq.Client.Abstractions.Diagnostics;
using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.Connection.Adapters;

internal class ConnectionAdapter : IConnectionAdapter
{
    private readonly IConnection connection;
    private readonly int channelsLimit;
    private readonly TimeSpan releaseTimeout;
    private readonly SemaphoreSlim semaphore;
    private bool disposed;

    public ConnectionAdapter(
        IConnection connection,
        int channelsLimit,
        TimeSpan releaseTimeout)
    {
        this.connection = connection;
        this.channelsLimit = channelsLimit;
        this.releaseTimeout = releaseTimeout;
        
        Id = Guid.NewGuid();
        semaphore = new SemaphoreSlim(channelsLimit, channelsLimit);
        connection.ConnectionShutdown += FireShutdownEvent!;
    }

    public Guid Id { get; }

    public decimal BusinessRatio => 1 - (decimal)semaphore.CurrentCount / channelsLimit;

    public ConnectionBusinessStatus Status => BusinessRatio switch
    {
        >= 1M => ConnectionBusinessStatus.Full,
        >= 0.8M => ConnectionBusinessStatus.Busy,
        >= 0.4M => ConnectionBusinessStatus.Working,
        > 0 => ConnectionBusinessStatus.Free,
        _ => ConnectionBusinessStatus.Idle
    };

    public IChannelAdapter OpenChannel()
    {
        if (!semaphore.Wait(releaseTimeout))
        {
            throw new ConnectionChannelsExceededException(channelsLimit);
        }

        var channel = connection.CreateModel();
        channel.ModelShutdown += FreeChannel!;
        Event.WriteIfEnabled(Diagnostics.ChannelOpen, new { Channel = channel });
        return new ChannelAdapter(channel);
    }

    private void FreeChannel(object sender, ShutdownEventArgs e)
    {
        semaphore.Release();
        var channel = (IModel)sender;
        channel.ModelShutdown -= FreeChannel!;
    }

    private void FireShutdownEvent(object sender, ShutdownEventArgs e)
    {
        Event.WriteIfEnabled(Diagnostics.ConnectionDisrupt, new { Connection = sender });
        OnDisrupted?.Invoke(this, new ConnectionDisruptedEventArgs());
    }

    public event EventHandler<ConnectionDisruptedEventArgs>? OnDisrupted;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        connection.ConnectionShutdown -= FireShutdownEvent!;
        connection.Dispose();
        
        semaphore.Dispose();

        disposed = true;
    }
}