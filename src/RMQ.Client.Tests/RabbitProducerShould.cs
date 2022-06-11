using Microsoft.Extensions.DependencyInjection;
using Moq;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Consuming;
using Xunit.Abstractions;

namespace RMQ.Client.Tests;

public class RabbitProducerShould : IClassFixture<RabbitProducerFixture>
{
    private readonly RabbitProducerFixture fixture;
    private readonly Mock<ITestCaller> caller;

    public RabbitProducerShould(
        RabbitProducerFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        caller = new Mock<ITestCaller>();
        caller.Setup(c => c.Call(It.IsAny<string>()));
        fixture.ServiceCollection.AddTransient<TestInterfaceMiddleware>();
        fixture.ServiceCollection.AddTransient<TestConventionMiddleware>();
        fixture.ServiceCollection.AddSingleton(new TestProcessor(testOutputHelper, caller.Object));
        fixture.ServiceCollection.AddSingleton(caller.Object);
    }

    [Fact]
    public async Task PublishEncodedMessage()
    {
        var consumerBuilder = fixture.GetConsumerBuilder();
        var rabbitConsumerParameters = new RabbitConsumerParameters(
            "test-consumer", "test-queue", ProcessingOrder.Sequential);

        var producerBuilder = fixture.GetProducerBuilder();
        using var producer = producerBuilder
            .BuildRabbit(new RabbitProducerParameters("test-exchange"));
        await producer.Send("whatever", "test-message", CancellationToken.None);

        using var consumer = consumerBuilder
            .WithMiddleware<TestInterfaceMiddleware>()
            .WithMiddleware<TestConventionMiddleware>(5)
            .BuildRabbit<TestProcessor, string>(rabbitConsumerParameters);

        consumer.Subscribe();

        await Task.Delay(1000);

        caller.Verify(c => c.Call("test-message"), Times.Once);
        caller.Verify(c => c.Call("middlewared test-message"), Times.Once);
        caller.Verify(c => c.Call("conventioned test-message 5"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    private class TestInterfaceMiddleware : IConsumerMiddleware
    {
        private readonly ITestCaller testCaller;

        public TestInterfaceMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task<ProcessResult> InvokeAsync<TMessage>(
            ConsumerContext<TMessage> context,
            ConsumerDelegate<TMessage> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(context.Message switch
            {
                string m => $"middlewared {m}",
                _ => string.Empty
            });
            return next(context, cancellationToken);
        }
    }
    
    private class TestConventionMiddleware
    {
        private readonly ConsumerDelegate next;
        private readonly int number;

        public TestConventionMiddleware(
            ConsumerDelegate next,
            int number)
        {
            this.next = next;
            this.number = number;
        }

        public Task<ProcessResult> InvokeAsync<TMessage>(
            ConsumerContext<TMessage> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            context.StoredValues["test"] = 1;
            testCaller.Call(context.Message switch
            {
                string m => $"conventioned {m} {number}",
                _ => string.Empty
            });
            return next.Invoke(context, cancellationToken);
        }
    }

    private class TestProcessor : IProcessor<string>
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly ITestCaller caller;

        public TestProcessor(
            ITestOutputHelper testOutputHelper,
            ITestCaller caller)
        {
            this.testOutputHelper = testOutputHelper;
            this.caller = caller;
        }

        public Task<ProcessResult> Process(string message, CancellationToken cancellationToken)
        {
            testOutputHelper.WriteLine($"Incoming: {message}");
            caller.Call(message);
            return Task.FromResult(ProcessResult.Success);
        }
    }

    public interface ITestCaller
    {
        void Call(string message);
    }
}