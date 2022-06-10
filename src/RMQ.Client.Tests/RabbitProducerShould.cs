using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;

namespace RMQ.Client.Tests;

public class RabbitProducerShould : IClassFixture<RabbitProducerFixture>
{
    private readonly RabbitProducerFixture fixture;

    public RabbitProducerShould(
        RabbitProducerFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task PublishEncodedMessage()
    {
        var producerBuilder = fixture.GetProducerBuilder();
        using var producer = producerBuilder.BuildRabbit(new RabbitProducerParameters("test-exchange"));

        var serviceProvider = fixture.GetServiceProvider();
        var connectionFactory = serviceProvider.GetRequiredService<IConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        await producer.Send("whatever", "test-message", CancellationToken.None);

        await Task.Delay(100);
        var getResult = channel.BasicGet("test-queue", true);
        getResult.Should().NotBeNull();

        JsonSerializer.Deserialize<string>(getResult.Body.Span).Should().Be("test-message");
    }
}