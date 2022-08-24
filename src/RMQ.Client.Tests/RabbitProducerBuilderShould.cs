using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Exceptions;
using RMQ.Client.Abstractions.Producing;

namespace RMQ.Client.Tests;

public class RabbitProducerBuilderShould : IClassFixture<RabbitFixture>
{
    private readonly RabbitFixture fixture;
    private readonly Mock<ITestCaller> caller;

    public RabbitProducerBuilderShould(
        RabbitFixture fixture)
    {
        this.fixture = fixture;
        caller = new Mock<ITestCaller>();
        caller.Setup(c => c.Call(It.IsAny<string>()));
        fixture.ServiceCollection.AddSingleton(caller.Object);
        fixture.ServiceCollection.AddScoped<ClientAgnosticInterfacedMiddleware>();
        fixture.ServiceCollection.AddScoped<ClientSpecificInterfacedMiddleware>();
        fixture.ServiceCollection.AddScoped<ClientSpecificWrongInterfacedMiddleware>();
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
        var producerBuilder = fixture.GetProducerBuilder()
            .WithMiddleware(middleware);
        producerBuilder.Invoking(b => b.BuildRabbit(new RabbitProducerParameters("test-exchange")))
            .Should().Throw<ProducerBuilderMiddlewareConventionException>();
    }

    [Fact(Skip = "No integration tests yet")]
    public async Task IncludeAllMatchingMiddlewares()
    {
        fixture.CreateTopology();

        var producerBuilder = fixture.GetProducerBuilder();
        using var producer = producerBuilder
            .With(next => (context, ct) =>
            {
                caller.Object.Call("ClientAgnosticLambdaMiddleware");
                return next(context, ct);
            })
            .With<IBasicProperties>(next => (context, ct) =>
            {
                caller.Object.Call("ClientSpecificLambdaMiddleware");
                return next(context, ct);
            })
            .WithMiddleware<ClientAgnosticInterfacedMiddleware>()
            .WithMiddleware<ClientSpecificInterfacedMiddleware>()
            .WithMiddleware<ClientAgnosticConventionalMiddleware>()
            .WithMiddleware<ClientSpecificConventionalMiddleware>()
            .WithMiddleware<ClientSpecificWrongInterfacedMiddleware>()
            .WithMiddleware(typeof(GenericClientSpecificConventionalMiddleware<>))
            .WithMiddleware<GenericClientSpecificConventionalMiddleware>()
            .BuildRabbit(new RabbitProducerParameters("test-exchange"));
        await producer.Send("whatever", "test-message", CancellationToken.None);

        caller.Verify(c => c.Call("ClientAgnosticLambdaMiddleware"));
        caller.Verify(c => c.Call("ClientSpecificLambdaMiddleware"));
        caller.Verify(c => c.Call("ClientAgnosticInterfacedMiddleware"));
        caller.Verify(c => c.Call("ClientSpecificInterfacedMiddleware"));
        caller.Verify(c => c.Call("ClientAgnosticConventionalMiddleware"));
        caller.Verify(c => c.Call("ClientSpecificConventionalMiddleware"));
        caller.Verify(c => c.Call("GenericClientSpecificConventionalMiddleware with IBasicProperties"));
        caller.Verify(c => c.Call("GenericClientSpecificConventionalMiddleware"));
        caller.VerifyNoOtherCalls();

        fixture.ClearTopology();
    }

    private class ClientAgnosticInterfacedMiddleware : IProducerMiddleware
    {
        private readonly ITestCaller testCaller;

        public ClientAgnosticInterfacedMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task InvokeAsync(ProducerContext context, ProducerDelegate next, CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientAgnosticInterfacedMiddleware));
            return next(context, cancellationToken);
        }
    }

    private class ClientSpecificInterfacedMiddleware : IProducerMiddleware<IBasicProperties>
    {
        private readonly ITestCaller testCaller;

        public ClientSpecificInterfacedMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task InvokeAsync(
            ProducerContext<IBasicProperties> context,
            ProducerDelegate<IBasicProperties> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientSpecificInterfacedMiddleware));
            return next(context, cancellationToken);
        }
    }

    private class ClientSpecificWrongInterfacedMiddleware : IProducerMiddleware<string>
    {
        private readonly ITestCaller testCaller;

        public ClientSpecificWrongInterfacedMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task InvokeAsync(
            ProducerContext<string> context,
            ProducerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientSpecificWrongInterfacedMiddleware));
            return next(context, cancellationToken);
        }
    }

    private class ClientAgnosticConventionalMiddleware
    {
        private readonly ProducerDelegate next;

        public ClientAgnosticConventionalMiddleware(
            ProducerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync(
            ProducerContext context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientAgnosticConventionalMiddleware));
            return next(context, cancellationToken);
        }
    }

    private class ClientSpecificConventionalMiddleware
    {
        private readonly ProducerDelegate<IBasicProperties> next;

        public ClientSpecificConventionalMiddleware(
            ProducerDelegate<IBasicProperties> next)
        {
            this.next = next;
        }

        public Task InvokeAsync(
            ProducerContext<IBasicProperties> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            if (context.NativeProperties is not null)
            {
                context.NativeProperties.Persistent = true;
            }

            testCaller.Call(nameof(ClientSpecificConventionalMiddleware));
            return next(context, cancellationToken);
        }
    }

    private class GenericClientSpecificConventionalMiddleware<TNativeProperties>
    {
        private readonly ProducerDelegate<TNativeProperties> next;

        public GenericClientSpecificConventionalMiddleware(ProducerDelegate<TNativeProperties> next)
        {
            this.next = next;
        }

        public Task InvokeAsync(
            ProducerContext<TNativeProperties> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(
                $"{nameof(GenericClientSpecificConventionalMiddleware<TNativeProperties>)} with {typeof(TNativeProperties).Name}");
            return next(context, cancellationToken);
        }
    }

    private class GenericClientSpecificConventionalMiddleware
    {
        private readonly ProducerDelegate next;

        public GenericClientSpecificConventionalMiddleware(ProducerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync<TNativeProperties>(
            ProducerContext<TNativeProperties> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(GenericClientSpecificConventionalMiddleware));
            return next(context, cancellationToken);
        }
    }

    private class InvalidConventionMiddleware_NoInvokeMethod
    {
        private readonly ProducerDelegate next;

        public InvalidConventionMiddleware_NoInvokeMethod(ProducerDelegate next)
        {
            this.next = next;
        }

        public Task NotInvoke(ProducerContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_AmbiguousInvokeMethods
    {
        private readonly ProducerDelegate next;

        public InvalidConventionMiddleware_AmbiguousInvokeMethods(ProducerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync(ProducerContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task InvokeAsync(ProducerContext context, ITestCaller testCaller, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_Context
    {
        private readonly ProducerDelegate next;

        public InvalidConventionMiddleware_MismatchParameters_Context(ProducerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync(string context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_CancellationToken
    {
        private readonly ProducerDelegate next;

        public InvalidConventionMiddleware_MismatchParameters_CancellationToken(ProducerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync(ProducerContext context, string cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_Number
    {
        private readonly ProducerDelegate next;

        public InvalidConventionMiddleware_MismatchParameters_Number(ProducerDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync() => Task.CompletedTask;
    }
    }