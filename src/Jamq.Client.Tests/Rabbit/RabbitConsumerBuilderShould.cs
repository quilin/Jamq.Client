﻿using System.Diagnostics;
using FluentAssertions;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Diagnostics;
using Jamq.Client.Abstractions.Exceptions;
using Jamq.Client.Rabbit.Consuming;
using Jamq.Client.Rabbit.Producing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit.Abstractions;

namespace Jamq.Client.Tests.Rabbit;

public class RabbitConsumerBuilderShould : IClassFixture<RabbitFixture>
{
    private readonly RabbitFixture fixture;
    private readonly Mock<ITestCaller> caller;

    public RabbitConsumerBuilderShould(
        RabbitFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        caller = new Mock<ITestCaller>();
        caller.Setup(c => c.Call(It.IsAny<string>()));
        fixture.ServiceCollection.AddSingleton(caller.Object);
        fixture.ServiceCollection.AddScoped<Processor>();
        fixture.ServiceCollection.AddScoped<SpecificWrongInterfacedMiddleware>();

        DiagnosticListener.AllListeners.Subscribe(new RabbitFixture.Subscriber(testOutputHelper));
        ActivitySource.AddActivityListener(new ActivityListener
        {
            ShouldListenTo = s => s.Name == Event.SourceName,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => testOutputHelper.WriteLine($"Activity {activity.DisplayName}:{activity.Id} started at {activity.StartTimeUtc:O}"),
            ActivityStopped = activity => testOutputHelper.WriteLine($"Activity {activity.DisplayName}:{activity.Id} stopped at {activity.StartTimeUtc + activity.Duration:O} (after {activity.Duration:g})"),
        });
    }

    private class Processor : IProcessor<string, RabbitMessage>
    {
        private readonly ITestCaller testCaller;

        public Processor(
            ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task<ProcessResult> Process(string key, RabbitMessage message, CancellationToken cancellationToken)
        {
            testCaller.Call(message.Text);
            return Task.FromResult(ProcessResult.Success);
        }
    }

    private record RabbitMessage(string Text);

    private class InvalidConventionMiddleware_NoInvokeMethod
    {
        public InvalidConventionMiddleware_NoInvokeMethod(ConsumerDelegate next)
        {
        }

        public Task NotInvoke(ConsumerContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_AmbiguousInvokeMethods
    {
        public InvalidConventionMiddleware_AmbiguousInvokeMethods(ConsumerDelegate next)
        {
        }

        public Task InvokeAsync(ConsumerContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task InvokeAsync(ConsumerContext context, ITestCaller testCaller, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_Context
    {
        public InvalidConventionMiddleware_MismatchParameters_Context(ConsumerDelegate next)
        {
        }

        public Task InvokeAsync(string context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_CancellationToken
    {
        public InvalidConventionMiddleware_MismatchParameters_CancellationToken(ConsumerDelegate next)
        {
        }

        public Task InvokeAsync(ConsumerContext context, string cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_Number
    {
        public InvalidConventionMiddleware_MismatchParameters_Number(ConsumerDelegate next)
        {
        }

        public Task InvokeAsync() => Task.CompletedTask;
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
        producerBuilder.Invoking(b => b.BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("consumer", "test-exchange", ProcessingOrder.Sequential)))
            .Should().Throw<ConsumerBuilderMiddlewareConventionException>();
    }

    [Fact]
    public async Task IncludeClientAgnosticLambdaMiddleware()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .With(next => (context, token) =>
            {
                var testCaller = context.ServiceProvider.GetRequiredService<ITestCaller>();
                testCaller.Call("sut");
                return next.Invoke(context, token);
            })
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("sut"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IncludeMessageAgnosticLambdaMiddleware()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .With<RabbitConsumerProperties>(next => (context, token) =>
            {
                var testCaller = context.ServiceProvider.GetRequiredService<ITestCaller>();
                testCaller.Call("sut");
                return next.Invoke(context, token);
            })
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("sut"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IncludeSpecificLambdaMiddleware()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .With<string, RabbitMessage, RabbitConsumerProperties>(next => (context, token) =>
            {
                var testCaller = context.ServiceProvider.GetRequiredService<ITestCaller>();
                testCaller.Call("sut");
                return next.Invoke(context, token);
            })
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("sut"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
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

    [Fact]
    public async Task IncludeClientAgnosticInterfaceMiddleware()
    {
        fixture.ServiceCollection.AddScoped<ClientAgnosticInterfacedMiddleware>();
        using var consumer = fixture.GetConsumerBuilder()
            .WithMiddleware<ClientAgnosticInterfacedMiddleware>()
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("ClientAgnosticInterfacedMiddleware"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    private class MessageAgnosticInterfacedMiddleware : IConsumerMiddleware<RabbitConsumerProperties>
    {
        private readonly ITestCaller testCaller;

        public MessageAgnosticInterfacedMiddleware(
            ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<RabbitConsumerProperties> context,
            ConsumerDelegate<RabbitConsumerProperties> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(MessageAgnosticInterfacedMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }

    [Fact]
    public async Task IncludeMessageAgnosticInterfaceMiddleware()
    {
        fixture.ServiceCollection.AddScoped<MessageAgnosticInterfacedMiddleware>();
        using var consumer = fixture.GetConsumerBuilder()
            .WithMiddleware<MessageAgnosticInterfacedMiddleware>()
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("MessageAgnosticInterfacedMiddleware"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    private class SpecificInterfacedMiddleware : IConsumerMiddleware<string, RabbitMessage, RabbitConsumerProperties>
    {
        private readonly ITestCaller testCaller;

        public SpecificInterfacedMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<string, RabbitMessage, RabbitConsumerProperties> context,
            ConsumerDelegate<string, RabbitMessage, RabbitConsumerProperties> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(SpecificInterfacedMiddleware));
            return next(context, cancellationToken);
        }
    }

    [Fact]
    public async Task IncludeSpecificInterfaceMiddleware()
    {
        fixture.ServiceCollection.AddScoped<SpecificInterfacedMiddleware>();
        using var consumer = fixture.GetConsumerBuilder()
            .WithMiddleware<SpecificInterfacedMiddleware>()
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("SpecificInterfacedMiddleware"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
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

    [Fact]
    public async Task IncludeClientAgnosticConventionalMiddleware()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .WithMiddleware<ClientAgnosticConventionalMiddleware>()
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("ClientAgnosticConventionalMiddleware"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    private class MessageAgnosticConventionalMiddleware
    {
        private readonly ConsumerDelegate<RabbitConsumerProperties> next;

        public MessageAgnosticConventionalMiddleware(ConsumerDelegate<RabbitConsumerProperties> next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<RabbitConsumerProperties> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(MessageAgnosticConventionalMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }

    [Fact]
    public async Task IncludeMessageAgnosticConventionalMiddleware()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .WithMiddleware<MessageAgnosticConventionalMiddleware>()
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("MessageAgnosticConventionalMiddleware"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    private class SpecificConventionalMiddleware
    {
        private readonly ConsumerDelegate<string, RabbitMessage, RabbitConsumerProperties> next;

        public SpecificConventionalMiddleware(
            ConsumerDelegate<string, RabbitMessage, RabbitConsumerProperties> next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<string, RabbitMessage, RabbitConsumerProperties> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(SpecificConventionalMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }

    [Fact]
    public async Task IncludeSpecificConventionalMiddleware()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .WithMiddleware<SpecificConventionalMiddleware>()
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("SpecificConventionalMiddleware"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    private class GenericClientSpecificConventionalMiddleware<TKey, TMessage, TNativeProperties>
    {
        private readonly ConsumerDelegate<TKey, TMessage, TNativeProperties> next;

        public GenericClientSpecificConventionalMiddleware(
            ConsumerDelegate<TKey, TMessage, TNativeProperties> next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync(
            ConsumerContext<TKey, TMessage, TNativeProperties> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(
                $"{nameof(GenericClientSpecificConventionalMiddleware<TKey, TMessage, TNativeProperties>)} with {typeof(TNativeProperties).Name}, {typeof(TMessage).Name}");
            return next.Invoke(context, cancellationToken);
        }
    }

    [Fact]
    public async Task IncludeGenericClientSpecificConventionalMiddleware()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .WithMiddleware(typeof(GenericClientSpecificConventionalMiddleware<,,>))
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("GenericClientSpecificConventionalMiddleware with RabbitConsumerProperties, RabbitMessage"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    private class GenericClientSpecificConventionalMiddleware
    {
        private readonly ConsumerDelegate next;

        public GenericClientSpecificConventionalMiddleware(
            ConsumerDelegate next)
        {
            this.next = next;
        }

        public Task<ProcessResult> InvokeAsync<TKey, TMessage, TNativeProperties>(
            ConsumerContext<TKey, TMessage, TNativeProperties> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(GenericClientSpecificConventionalMiddleware));
            return next.Invoke(context, cancellationToken);
        }
    }

    [Fact]
    public async Task IncludeGenericClientSpecificConventionalMiddleware_WithGenericEntryPoint()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .WithMiddleware<GenericClientSpecificConventionalMiddleware>()
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("GenericClientSpecificConventionalMiddleware"), Times.Once);
        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
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

    [Fact]
    public async Task NotIncludeSpecificConventionalMiddleware_WhenTypesNotMatch()
    {
        using var consumer = fixture.GetConsumerBuilder()
            .WithMiddleware<SpecificWrongInterfacedMiddleware>()
            .BuildRabbit<RabbitMessage, Processor>(
                new RabbitConsumerParameters("test", "queue", ProcessingOrder.Sequential));
        consumer.Subscribe();

        var producer = fixture.GetProducerBuilder().BuildRabbit<RabbitMessage>(new RabbitProducerParameters("exchange"));
        await producer.Send("key", new RabbitMessage("message"), CancellationToken.None);

        await Task.Delay(100);

        caller.Verify(c => c.Call("message"), Times.Once);
        caller.VerifyNoOtherCalls();
    }
}