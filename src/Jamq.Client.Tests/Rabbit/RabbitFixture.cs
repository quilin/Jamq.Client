using System.Diagnostics;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Diagnostics;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit.Abstractions;

namespace Jamq.Client.Tests.Rabbit;

public class RabbitFixture : IDisposable
{
    public IServiceCollection ServiceCollection { get; }
    public Lazy<IServiceProvider> ServiceProviderProvider => new(() => providerFactory.CreateServiceProvider(ServiceCollection)); 
    private readonly DefaultServiceProviderFactory providerFactory;
    private (IBasicConsumer consumer, string tag) activeConsumerData;
    private static int deliveryTag;

    public RabbitFixture()
    {
        providerFactory = new DefaultServiceProviderFactory();
        ServiceCollection = providerFactory.CreateBuilder(new ServiceCollection());
        ServiceCollection.AddJamqClient(config => config
            .UseRabbit(new RabbitConnectionParameters()));

        var connectionFactory = new Mock<IConnectionFactory>();
        var connection = new Mock<IConnection>();
        var channel = new Mock<IModel>();
        connectionFactory.Setup(f => f.CreateConnection()).Returns(connection.Object);
        connection.Setup(c => c.CreateModel()).Returns(channel.Object);
        connection.Setup(c => c.Dispose());

        channel.Setup(c => c.QueueDeclare(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>()))
            .Returns(new QueueDeclareOk("haha", 0, 0));
        channel.Setup(c => c.ExchangeDeclare(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));
        channel.Setup(c => c.QueueBind(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IDictionary<string, object>>()));
        channel.Setup(c => c.BasicPublish(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<IBasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()));

        var basicProperties = new Mock<IBasicProperties>();
        basicProperties.Setup(p => p.Headers).Returns(new Dictionary<string, object>());

        channel.Setup(c => c.CreateBasicProperties()).Returns(basicProperties.Object);
        channel.Setup(c => c.ConfirmSelect());
        channel.Setup(c => c.WaitForConfirmsOrDie(It.IsAny<TimeSpan>()));

        channel.Setup(c => c.BasicConsume(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IBasicConsumer>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>(
                (_, _, consumerTag, _, _, _, consumer) => activeConsumerData = (consumer, consumerTag))
            .Returns("whatever");
        channel.Setup(c => c.Dispose());
        channel.Setup(c => c.BasicPublish(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()))
            .Callback<string, string, bool, IBasicProperties, ReadOnlyMemory<byte>>(
                (exchange, routingKey, _, basicProperties, body) =>
                {
                    var (consumer, tag) = activeConsumerData;
                    (consumer as AsyncEventingBasicConsumer)?.HandleBasicDeliver(
                        tag, (ulong) Interlocked.Increment(ref deliveryTag), false, exchange,
                        routingKey, basicProperties, body);
                });

        ServiceCollection.AddSingleton(connectionFactory.Object);
    }

    public IProducerBuilder GetProducerBuilder() => ServiceProviderProvider.Value.GetRequiredService<IProducerBuilder>();

    public IConsumerBuilder GetConsumerBuilder() => ServiceProviderProvider.Value.GetRequiredService<IConsumerBuilder>();

    public void Dispose()
    {
    }

    internal class Subscriber : IObserver<DiagnosticListener>
    {
        private readonly ITestOutputHelper testOutputHelper;

        public Subscriber(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }
        
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == Event.SourceName)
            {
                value.Subscribe(new Listener(testOutputHelper));
            }
        }
    }

    private class Listener : IObserver<KeyValuePair<string, object?>>
    {
        private readonly ITestOutputHelper testOutputHelper;

        public Listener(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object?> keyValue) => testOutputHelper.WriteLine($"{keyValue.Key} - {keyValue.Value}");
    }
}