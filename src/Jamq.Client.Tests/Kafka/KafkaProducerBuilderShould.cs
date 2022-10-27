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
        fixture.ServiceCollection.AddSingleton(testOutputHelper);
        serviceProvider = fixture.ServiceProvider;
    }

    [Fact(Skip = "No kafka integration tests yet")]
    public async Task ConnectToKafka()
    {
        using var producer = serviceProvider.GetRequiredService<IProducerBuilder>()
            .With<KafkaProducerProperties<string, string>>(next => (context, token) =>
            {
                var message = context.NativeProperties.Message;
                testOutputHelper.WriteLine($"{message.Key}: {message.Value}");
                return next.Invoke(context, token);
            })
            .BuildKafka<string, string>((_, config) => 
                new KafkaProducerParameters(new ProducerConfig(config), "demo-topic"));

        await producer.Send("test", "message", CancellationToken.None);
    }

    [Fact(Skip = "No kafka integration tests yet")]
    public async Task ConsumeStuff()
    {
        using var consumer = serviceProvider.GetRequiredService<IConsumerBuilder>()
            .With<string, string, KafkaConsumerProperties<string, string>>(next => (context, token) =>
            {
                var message = context.NativeProperties.ConsumeResult.Message;
                testOutputHelper.WriteLine($"Consumed message with key: {message.Key}, body: {message.Value}");
                return next.Invoke(context, token);
            })
            .BuildKafka<string, string, TestProcessor>((_, config) =>
                new KafkaConsumerParameters(new ConsumerConfig(config)
                {
                    GroupId = Guid.NewGuid().ToString(),
                    AutoOffsetReset = AutoOffsetReset.Earliest
                }, "demo-topic"));

        consumer.Subscribe();

        await Task.Delay(TimeSpan.FromSeconds(10));
    }

    public class TestProcessor : IProcessor<string, string>
    {
        private readonly ITestOutputHelper testOutputHelper;

        public TestProcessor(
            ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public Task<ProcessResult> Process(string key, string message, CancellationToken cancellationToken)
        {
            testOutputHelper.WriteLine($"Processed message {key}: {message}");
            return Task.FromResult(ProcessResult.Success);
        }
    }
}