using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client.Events;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Consuming;

namespace RMQ.Client.Tests;

public class RabbitConsumerShould : IClassFixture<RabbitFixture>
{
    private readonly RabbitFixture fixture;
    private readonly Mock<RabbitProducerShould.ITestCaller> caller;

    public RabbitConsumerShould(
        RabbitFixture fixture)
    {
        this.fixture = fixture;
        caller = new Mock<RabbitProducerShould.ITestCaller>();
        caller.Setup(c => c.Call(It.IsAny<string>()));
        fixture.ServiceCollection.AddSingleton(caller.Object);
        fixture.ServiceCollection.AddScoped<Processor>();
        fixture.ServiceCollection.AddScoped<ClientAgnosticInterfacedMiddleware>();
        fixture.ServiceCollection.AddScoped<ClientSpecificInterfacedMiddleware>();
    }

    [Fact(Skip = "No integration tests yet")]
    public async Task ConsumeMessages()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .With(next => (context, token) =>
            {
                var testCaller = context.ServiceProvider.GetRequiredService<RabbitProducerShould.ITestCaller>();
                testCaller.Call("ClientAgnosticLambdaMiddleware");
                return next.Invoke(context, token);
            })
            .With<BasicDeliverEventArgs, RabbitMessage>(next => (context, token) =>
            {
                var testCaller = context.ServiceProvider.GetRequiredService<RabbitProducerShould.ITestCaller>();
                testCaller.Call("ClientSpecificLambdaMiddleware");
                return next.Invoke(context, token);
            })
            .WithMiddleware<ClientAgnosticInterfacedMiddleware>()
            .WithMiddleware<ClientSpecificInterfacedMiddleware>()
            .WithMiddleware<ClientSpecificWrongInterfacedMiddleware>()
            .WithMiddleware<ClientAgnosticConventionalMiddleware>()
            .WithMiddleware<ClientSpecificConventionalMiddleware>()
            .WithMiddleware<GenericClientSpecificConventionalMiddleware>()
            .WithMiddleware(typeof(GenericClientSpecificConventionalMiddleware<,>))
            .BuildRabbit<Processor, RabbitMessage>(
                new RabbitConsumerParameters("test", "test-queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        using var producer = fixture.GetProducerBuilder()
            .BuildRabbit(new RabbitProducerParameters("test-exchange"));
        await producer.Send("test", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(1));

        caller.Verify(c => c.Call("ClientAgnosticLambdaMiddleware"), Times.Once);
        caller.Verify(c => c.Call("ClientSpecificLambdaMiddleware"), Times.Once);
        caller.Verify(c => c.Call("ClientAgnosticInterfacedMiddleware"), Times.Once);
        caller.Verify(c => c.Call("ClientSpecificInterfacedMiddleware"), Times.Once);
        caller.Verify(c => c.Call("ClientAgnosticConventionalMiddleware"), Times.Once);
        caller.Verify(c => c.Call("ClientSpecificConventionalMiddleware"), Times.Once);
        caller.Verify(c => c.Call("GenericClientSpecificConventionalMiddleware"), Times.Once);
        caller.Verify(c => c.Call("GenericClientSpecificConventionalMiddleware with BasicDeliverEventArgs, RabbitMessage"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }
    
    private class ClientAgnosticInterfacedMiddleware : IConsumerMiddleware
    {
        private readonly RabbitProducerShould.ITestCaller testCaller;

        public ClientAgnosticInterfacedMiddleware(
            RabbitProducerShould.ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }
        
        public Task<ProcessResult> InvokeAsync(
            ConsumerContext context,
            ConsumerDelegate next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientAgnosticInterfacedMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }
    private class ClientSpecificInterfacedMiddleware : IConsumerMiddleware<BasicDeliverEventArgs>
    {
        private readonly RabbitProducerShould.ITestCaller testCaller;

        public ClientSpecificInterfacedMiddleware(
            RabbitProducerShould.ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }
        
        public Task<ProcessResult> InvokeAsync<TMessage>(
            ConsumerContext<BasicDeliverEventArgs, TMessage> context,
            ConsumerDelegate<BasicDeliverEventArgs, TMessage> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientSpecificInterfacedMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }
    private class ClientAgnosticConventionalMiddleware
    {
        private readonly ConsumerDelegate next;

        public ClientAgnosticConventionalMiddleware(ConsumerDelegate next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext context,
            RabbitProducerShould.ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientAgnosticConventionalMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }
    private class ClientSpecificConventionalMiddleware
    {
        private readonly ConsumerDelegate<BasicDeliverEventArgs, RabbitMessage> next;

        public ClientSpecificConventionalMiddleware(
            ConsumerDelegate<BasicDeliverEventArgs, RabbitMessage> next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<BasicDeliverEventArgs, RabbitMessage> context,
            RabbitProducerShould.ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientSpecificConventionalMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }
    private class ClientSpecificWrongInterfacedMiddleware : IConsumerMiddleware<string>
    {
        private readonly RabbitProducerShould.ITestCaller testCaller;

        public ClientSpecificWrongInterfacedMiddleware(
            RabbitProducerShould.ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task<ProcessResult> InvokeAsync<TMessage>(
            ConsumerContext<string, TMessage> context,
            ConsumerDelegate<string, TMessage> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call("Whatever");
            return next.Invoke(context, cancellationToken);
        }
    }
    private class GenericClientSpecificConventionalMiddleware
    {
        private readonly ConsumerDelegate next;

        public GenericClientSpecificConventionalMiddleware(
            ConsumerDelegate next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync<TNativeProperties, TMessage>(
            ConsumerContext<TNativeProperties, TMessage> context,
            RabbitProducerShould.ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(GenericClientSpecificConventionalMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }
    private class GenericClientSpecificConventionalMiddleware<TNativeProperties, TMessage>
    {
        private readonly ConsumerDelegate<TNativeProperties, TMessage> next;

        public GenericClientSpecificConventionalMiddleware(
            ConsumerDelegate<TNativeProperties, TMessage> next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<TNativeProperties, TMessage> context,
            RabbitProducerShould.ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call($"{nameof(GenericClientSpecificConventionalMiddleware<TNativeProperties, TMessage>)} with {typeof(TNativeProperties).Name}, {typeof(TMessage).Name}");
            return next.Invoke(context, cancellationToken);
        }
    }
    private class Processor : IProcessor<RabbitMessage>
    {
        private readonly RabbitProducerShould.ITestCaller testCaller;

        public Processor(
            RabbitProducerShould.ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task<ProcessResult> Process(RabbitMessage message, CancellationToken cancellationToken)
        {
            testCaller.Call(message.Text);
            return Task.FromResult(ProcessResult.Success);
        }
    }

    private record RabbitMessage(string Text);
}