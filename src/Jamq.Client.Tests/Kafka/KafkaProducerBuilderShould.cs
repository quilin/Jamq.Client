using Confluent.Kafka;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Kafka.Consuming;
using Jamq.Client.Kafka.Producing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Jamq.Client.Tests.Kafka;

public class KafkaProducerBuilderShould : IClassFixture<KafkaFixture>
{
    private readonly ITestOutputHelper testOutputHelper;
    private readonly IServiceProvider serviceProvider;

    public KafkaProducerBuilderShould(
        KafkaFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
        serviceProvider = fixture.ServiceProvider;
    }

    [Fact(Skip = "No kafka integration tests yet")]
    public async Task ConnectToKafka()
    {
        using var producer = serviceProvider.GetRequiredService<IProducerBuilder>()
            .With<string, string, Message<string, string>>(next => (context, token) =>
            {
                testOutputHelper.WriteLine($"{context.Key}: {context.Message}");
                return next.Invoke(context, token);
            })
            .BuildKafka<string, string>((_, config) => 
                new KafkaProducerParameters(new ProducerConfig(config), "test"));

        await producer.Send("test", "message", CancellationToken.None);
    }

    [Fact(Skip = "No kafka integration tests yet")]
    public async Task ConsumeStuff()
    {
        using var consumer = serviceProvider.GetRequiredService<IConsumerBuilder>()
            .With<string, string, ConsumeResult<string, string>>(next => (context, token) =>
            {
                testOutputHelper.WriteLine($"Consumed message with key: {context.Key}, body: {context.Message}");
                return next.Invoke(context, token);
            })
            .BuildKafka<string, string, TestProcessor>((_, config) =>
                new KafkaConsumerParameters(new ConsumerConfig(config)
                {
                    GroupId = "ugh"
                }, "test"));
        
        consumer.Subscribe();

        await Task.Delay(300000);
    }
    
    public class TestProcessor : IProcessor<string, string>
    {
        public Task<ProcessResult> Process(string key, string message, CancellationToken cancellationToken) => 
            Task.FromResult(ProcessResult.Success);
    }
}