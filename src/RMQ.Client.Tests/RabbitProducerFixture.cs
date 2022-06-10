using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.DependencyInjection;

namespace RMQ.Client.Tests;

public class RabbitProducerFixture
{
    public IServiceCollection ServiceCollection { get; }
    private readonly DefaultServiceProviderFactory providerFactory;

    public RabbitProducerFixture()
    {
        providerFactory = new DefaultServiceProviderFactory();
        ServiceCollection = providerFactory.CreateBuilder(new ServiceCollection());
        ServiceCollection.AddRmqClient(new RabbitConnectionParameters());

        var connectionFactory = providerFactory
            .CreateServiceProvider(ServiceCollection)
            .GetRequiredService<IConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare("test-exchange", "topic", true);
        channel.QueueDeclare("test-queue", true, false);
        channel.QueueBind("test-queue", "test-exchange", "#");
    }

    public IProducerBuilder GetProducerBuilder() => providerFactory.CreateServiceProvider(ServiceCollection)
        .GetRequiredService<IProducerBuilder>();

    public IServiceProvider GetServiceProvider() => providerFactory.CreateServiceProvider(ServiceCollection);
}