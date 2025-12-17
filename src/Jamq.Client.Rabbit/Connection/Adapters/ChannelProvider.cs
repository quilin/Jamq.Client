using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.Connection.Adapters;

internal class ChannelProvider : IConsumerChannelProvider, IProducerChannelProvider
{
    private readonly IConnectionFactory connectionFactory;
    private Lazy<IConnection> connectionProvider;

    public ChannelProvider(IConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
        connectionProvider = new(EstablishConnection, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private IConnection EstablishConnection()
    {
        var connection = connectionFactory.CreateConnection();
        connection.ConnectionShutdown += RecoverConnection;
        return connection;
    }

    private void RecoverConnection(object sender, ShutdownEventArgs e)
    {
        lock (connectionProvider)
        {
            CloseCurrentConnection();
            connectionProvider = new(EstablishConnection, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    private void CloseCurrentConnection()
    {
        if (!connectionProvider.IsValueCreated) return;
        var connection = connectionProvider.Value;
        connection.ConnectionShutdown -= RecoverConnection;
        connection.Close();
        connection.Dispose();
    }

    public IChannelAdapter Get() =>
        new RobustChannelAdapter(connectionProvider.Value.CreateModel());

    public void Dispose()
    {
        CloseCurrentConnection();
    }
}