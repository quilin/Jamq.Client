﻿using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client.Events;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Consuming;
using RMQ.Client.Abstractions.Exceptions;

namespace RMQ.Client.Tests;

public class RabbitConsumerBuilderShould : IClassFixture<RabbitFixture>
{
    private readonly RabbitFixture fixture;
    private readonly Mock<ITestCaller> caller;

    public RabbitConsumerBuilderShould(
        RabbitFixture fixture)
    {
        this.fixture = fixture;
        caller = new Mock<ITestCaller>();
        caller.Setup(c => c.Call(It.IsAny<string>()));
        fixture.ServiceCollection.AddSingleton(caller.Object);
        fixture.ServiceCollection.AddScoped<Processor>();
        fixture.ServiceCollection.AddScoped<ClientAgnosticInterfacedMiddleware>();
        fixture.ServiceCollection.AddScoped<MessageAgnosticInterfacedMiddleware>();
        fixture.ServiceCollection.AddScoped<SpecificWrongInterfacedMiddleware>();
        fixture.ServiceCollection.AddScoped<SpecificInterfacedMiddleware>();
    }

    public static IEnumerable<object[]> InvalidMiddlewares()
    {
        yield return new object[] {typeof(InvalidConventionMiddleware_NoInvokeMethod)};
        yield return new object[] {typeof(InvalidConventionMiddleware_AmbiguousInvokeMethods)};
        yield return new object[] {typeof(InvalidConventionMiddleware_MismatchParameters_Number)};
        yield return new object[] {typeof(InvalidConventionMiddleware_MismatchParameters_Context)};
        yield return new object[] {typeof(InvalidConventionMiddleware_MismatchParameters_CancellationToken)};
    }

    [Theory]
    [MemberData(nameof(InvalidMiddlewares))]
    public void Throw_WhenInvalidMiddlewaresAdded(Type middleware)
    {
        var producerBuilder = fixture.GetConsumerBuilder()
            .WithMiddleware(middleware);
        producerBuilder.Invoking(b => b.BuildRabbit<Processor, RabbitMessage>(
                new RabbitConsumerParameters("consumer", "test-exchange", ProcessingOrder.Sequential)))
            .Should().Throw<ConsumerBuilderMiddlewareConventionException>();
    }

    [Fact(Skip = "No integration tests yet")]
    public async Task IncludeAllMatchingMiddlewares()
    {
        fixture.CreateTopology();

        using (var consumer = fixture.GetConsumerBuilder()
            .With(next => (context, token) =>
            {
                var testCaller = context.ServiceProvider.GetRequiredService<ITestCaller>();
                testCaller.Call("ClientAgnosticLambdaMiddleware");
                return next.Invoke(context, token);
            })
            .With<BasicDeliverEventArgs>(next => (context, token) =>
            {
                var testCaller = context.ServiceProvider.GetRequiredService<ITestCaller>();
                testCaller.Call("MessageAgnosticLambdaMiddleware");
                return next.Invoke(context, token);
            })
            .With<BasicDeliverEventArgs, RabbitMessage>(next => (context, token) =>
            {
                var testCaller = context.ServiceProvider.GetRequiredService<ITestCaller>();
                testCaller.Call("SpecificLambdaMiddleware");
                return next.Invoke(context, token);
            })
            .WithMiddleware<ClientAgnosticInterfacedMiddleware>()
            .WithMiddleware<MessageAgnosticInterfacedMiddleware>()
            .WithMiddleware<SpecificInterfacedMiddleware>()
            .WithMiddleware<SpecificWrongInterfacedMiddleware>()
            .WithMiddleware<ClientAgnosticConventionalMiddleware>()
            .WithMiddleware<MessageAgnosticConventionalMiddleware>()
            .WithMiddleware<SpecificConventionalMiddleware>()
            .WithMiddleware<GenericClientSpecificConventionalMiddleware>()
            .WithMiddleware(typeof(GenericClientSpecificConventionalMiddleware<,>))
            .BuildRabbit<Processor, RabbitMessage>(
                new RabbitConsumerParameters("test", "test-queue", ProcessingOrder.Sequential)))
        {
            consumer.Subscribe();

            using var producer = fixture.GetProducerBuilder()
                .BuildRabbit(new RabbitProducerParameters("test-exchange"));
            await producer.Send("test", new RabbitMessage("message"), CancellationToken.None);

            await Task.Delay(TimeSpan.FromSeconds(1));

            caller.Verify(c => c.Call("ClientAgnosticLambdaMiddleware"), Times.Once);
            caller.Verify(c => c.Call("MessageAgnosticLambdaMiddleware"), Times.Once);
            caller.Verify(c => c.Call("SpecificLambdaMiddleware"), Times.Once);
            caller.Verify(c => c.Call("ClientAgnosticInterfacedMiddleware"), Times.Once);
            caller.Verify(c => c.Call("MessageAgnosticInterfacedMiddleware"), Times.Once);
            caller.Verify(c => c.Call("SpecificInterfacedMiddleware"), Times.Once);
            caller.Verify(c => c.Call("ClientAgnosticConventionalMiddleware"), Times.Once);
            caller.Verify(c => c.Call("MessageAgnosticConventionalMiddleware"), Times.Once);
            caller.Verify(c => c.Call("SpecificConventionalMiddleware"), Times.Once);
            caller.Verify(c => c.Call("GenericClientSpecificConventionalMiddleware"), Times.Once);
            caller.Verify(
                c => c.Call("GenericClientSpecificConventionalMiddleware with BasicDeliverEventArgs, RabbitMessage"),
                Times.Once);
            caller.Verify(c => c.Call("message"), Times.Once);
            caller.VerifyNoOtherCalls();
        }

        fixture.ClearTopology();
    }
    
    private class ClientAgnosticInterfacedMiddleware : IConsumerMiddleware
    {
        private readonly ITestCaller testCaller;

        public ClientAgnosticInterfacedMiddleware(
            ITestCaller testCaller)
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
    private class MessageAgnosticInterfacedMiddleware : IConsumerMiddleware<BasicDeliverEventArgs>
    {
        private readonly ITestCaller testCaller;

        public MessageAgnosticInterfacedMiddleware(
            ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }
        
        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<BasicDeliverEventArgs> context,
            ConsumerDelegate<BasicDeliverEventArgs> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(MessageAgnosticInterfacedMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }
    private class SpecificInterfacedMiddleware : IConsumerMiddleware<BasicDeliverEventArgs, RabbitMessage>
    {
        private readonly ITestCaller testCaller;

        public SpecificInterfacedMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }
        
        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<BasicDeliverEventArgs, RabbitMessage> context,
            ConsumerDelegate<BasicDeliverEventArgs, RabbitMessage> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(SpecificInterfacedMiddleware));
            return next(context, cancellationToken);
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
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientAgnosticConventionalMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }
    private class MessageAgnosticConventionalMiddleware
    {
        private readonly ConsumerDelegate<BasicDeliverEventArgs> next;

        public MessageAgnosticConventionalMiddleware(ConsumerDelegate<BasicDeliverEventArgs> next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<BasicDeliverEventArgs> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(MessageAgnosticConventionalMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }
    private class SpecificConventionalMiddleware
    {
        private readonly ConsumerDelegate<BasicDeliverEventArgs, RabbitMessage> next;

        public SpecificConventionalMiddleware(
            ConsumerDelegate<BasicDeliverEventArgs, RabbitMessage> next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<BasicDeliverEventArgs, RabbitMessage> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(SpecificConventionalMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }
    private class SpecificWrongInterfacedMiddleware : IConsumerMiddleware<string>
    {
        private readonly ITestCaller testCaller;

        public SpecificWrongInterfacedMiddleware(
            ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<string> context,
            ConsumerDelegate<string> next,
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
            ITestCaller testCaller,
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
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call($"{nameof(GenericClientSpecificConventionalMiddleware<TNativeProperties, TMessage>)} with {typeof(TNativeProperties).Name}, {typeof(TMessage).Name}");
            return next.Invoke(context, cancellationToken);
        }
    }
    private class Processor : IProcessor<RabbitMessage>
    {
        private readonly ITestCaller testCaller;

        public Processor(
            ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task<ProcessResult> Process(RabbitMessage message, CancellationToken cancellationToken)
        {
            testCaller.Call(message.Text);
            return Task.FromResult(ProcessResult.Success);
        }
    }
    private class InvalidConventionMiddleware_NoInvokeMethod
    {
        private readonly ConsumerDelegate next;

        public InvalidConventionMiddleware_NoInvokeMethod(ConsumerDelegate next)
        {
            this.next = next;
        }

        public Task NotInvoke(ConsumerContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_AmbiguousInvokeMethods
    {
        private readonly ConsumerDelegate next;

        public InvalidConventionMiddleware_AmbiguousInvokeMethods(ConsumerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync(ConsumerContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task InvokeAsync(ConsumerContext context, ITestCaller testCaller, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_Context
    {
        private readonly ConsumerDelegate next;

        public InvalidConventionMiddleware_MismatchParameters_Context(ConsumerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync(string context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_CancellationToken
    {
        private readonly ConsumerDelegate next;

        public InvalidConventionMiddleware_MismatchParameters_CancellationToken(ConsumerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync(ConsumerContext context, string cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_Number
    {
        private readonly ConsumerDelegate next;

        public InvalidConventionMiddleware_MismatchParameters_Number(ConsumerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync() => Task.CompletedTask;
    }

    private record RabbitMessage(string Text);
}