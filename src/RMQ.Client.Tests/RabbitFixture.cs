using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Consuming;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.DependencyInjection;

namespace RMQ.Client.Tests;

public class RabbitFixture : IDisposable
{
    public IServiceCollection ServiceCollection { get; }
    private readonly DefaultServiceProviderFactory providerFactory;

    public RabbitFixture()
    {
        providerFactory = new DefaultServiceProviderFactory();
        ServiceCollection = providerFactory.CreateBuilder(new ServiceCollection());
        ServiceCollection.AddRmqClient(new RabbitConnectionParameters());

        var connectionFactory = GetServiceProvider().GetRequiredService<IConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare("test-exchange", "topic", true);
        channel.QueueDeclare("test-queue", true, false, false);
        channel.QueueBind("test-queue", "test-exchange", "#");
    }

    public IProducerBuilder GetProducerBuilder() => providerFactory.CreateServiceProvider(ServiceCollection)
        .GetRequiredService<IProducerBuilder>();

    public IConsumerBuilder GetConsumerBuilder() => providerFactory.CreateServiceProvider(ServiceCollection)
        .GetRequiredService<IConsumerBuilder>();

    private IServiceProvider GetServiceProvider() => providerFactory.CreateServiceProvider(ServiceCollection);

    public void Dispose()
    {
        var connectionFactory = GetServiceProvider().GetRequiredService<IConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDelete("test-queue");
        channel.ExchangeDelete("test-exchange");
    }
}