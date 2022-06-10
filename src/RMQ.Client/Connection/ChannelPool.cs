using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Connection.Adapters;

namespace RMQ.Client.Connection;

internal class ChannelPool : IProducerChannelPool
{
    private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(30);

    private readonly ICollection<IConnectionAdapter> connections = new List<IConnectionAdapter>();
    private readonly IConnectionFactory connectionFactory;
    private readonly RabbitConnectionParameters parameters;

    private bool disposed;

    public ChannelPool(
        IConnectionFactory connectionFactory,
        RabbitConnectionParameters parameters)
    {
        this.connectionFactory = connectionFactory;
        this.parameters = parameters;
    }

    public IChannelAdapter Get()
    {
        lock (connections)
        {
            var currentConnections = connections
                .GroupBy(c => c.Status)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.BusinessRatio).ToArray());

            if (currentConnections.TryGetValue(ConnectionBusinessStatus.Idle, out var idleConnections))
            {
                foreach (var connection in idleConnections.Skip(1))
                {
                    DisposeConnection(connection);
                    connections.Remove(connection);
                }

                return idleConnections.First().OpenChannel();
            }

            if (currentConnections.TryGetValue(ConnectionBusinessStatus.Working, out var workingConnections))
            {
                return workingConnections.First().OpenChannel();
            }

            if (currentConnections.TryGetValue(ConnectionBusinessStatus.Free, out var freeConnections))
            {
                return freeConnections.First().OpenChannel();
            }

            if (connections.Count < parameters.PoolSize)
            {
                var connection = CreateConnection();
                connection.OnDisrupted += RemoveAndDisposeConnection!;
                connections.Add(connection);
                return connection.OpenChannel();
            }

            if (currentConnections.TryGetValue(ConnectionBusinessStatus.Busy, out var busyConnections))
            {
                return busyConnections.First().OpenChannel();
            }

            throw new ChannelPoolExhaustedException(parameters.PoolSize);
        }
    }

    private IConnectionAdapter CreateConnection() => new ConnectionAdapter(
        connectionFactory.CreateConnection(), parameters.ChannelsLimit, ReleaseTimeout);

    private void RemoveAndDisposeConnection(object sender, ConnectionDisruptedEventArgs e)
    {
        lock (connections)
        {
            var connection = (IConnectionAdapter)sender;
            DisposeConnection(connection);
            connections.Remove(connection);
        }
    }

    private void DisposeConnection(IConnectionAdapter connection)
    {
        lock (connections)
        {
            connection.OnDisrupted -= RemoveAndDisposeConnection!;
            connection.Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        lock (connections)
        {
            if (disposed)
            {
                return;
            }

            foreach (var connection in connections)
            {
                DisposeConnection(connection);
            }

            connections.Clear();
            disposed = true;
        }
    }
}