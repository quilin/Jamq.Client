using FluentAssertions;
using Jamq.Client.Abstractions.Exceptions;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Jamq.Client.Tests.Rabbit;

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
    }

    private class InvalidConventionMiddleware_NoInvokeMethod
    {
        public InvalidConventionMiddleware_NoInvokeMethod(ProducerDelegate next)
        {
        }

        public Task NotInvoke(ProducerContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_AmbiguousInvokeMethods
    {
        public InvalidConventionMiddleware_AmbiguousInvokeMethods(ProducerDelegate next)
        {
        }

        public Task InvokeAsync(ProducerContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task InvokeAsync(ProducerContext context, ITestCaller testCaller, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_Context
    {
        public InvalidConventionMiddleware_MismatchParameters_Context(ProducerDelegate next)
        {
        }

        public Task InvokeAsync(string context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_CancellationToken
    {
        public InvalidConventionMiddleware_MismatchParameters_CancellationToken(ProducerDelegate next)
        {
        }

        public Task InvokeAsync(ProducerContext context, string cancellationToken) => Task.CompletedTask;
    }

    private class InvalidConventionMiddleware_MismatchParameters_Number
    {
        public InvalidConventionMiddleware_MismatchParameters_Number(ProducerDelegate next)
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
        var producerBuilder = fixture.GetProducerBuilder()
            .WithMiddleware(middleware);
        producerBuilder.Invoking(b => b.BuildRabbit<string>(new RabbitProducerParameters("test-exchange")))
            .Should().Throw<ProducerBuilderMiddlewareConventionException>();
    }

    [Fact]
    public async Task IncludeClientAgnosticLambdaMiddleware()
    {
        using var producer = fixture.GetProducerBuilder()
            .With(next => (context, ct) =>
            {
                caller.Object.Call("sut");
                return next(context, ct);
            })
            .BuildRabbit<string>(new RabbitProducerParameters("test"));
        await producer.Send("whatever", "test message", CancellationToken.None);

        caller.Verify(c => c.Call("sut"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IncludeMessageAgnosticLambdaMiddleware()
    {
        using var producer = fixture.GetProducerBuilder()
            .With<RabbitProducerProperties>(next => (context, ct) =>
            {
                caller.Object.Call("sut");
                return next(context, ct);
            })
            .BuildRabbit<string>(new RabbitProducerParameters("test"));
        await producer.Send("whatever", "test message", CancellationToken.None);

        caller.Verify(c => c.Call("sut"), Times.Once);
        caller.VerifyNoOtherCalls();
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

    [Fact]
    public async Task IncludeClientAgnosticInterfaceMiddleware()
    {
        fixture.ServiceCollection.AddScoped<ClientAgnosticInterfacedMiddleware>();
        using var producer = fixture.GetProducerBuilder()
            .WithMiddleware<ClientAgnosticInterfacedMiddleware>()
            .BuildRabbit<string>(new RabbitProducerParameters("test"));
        await producer.Send("whatever", "test message", CancellationToken.None);

        caller.Verify(c => c.Call("ClientAgnosticInterfacedMiddleware"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    private class ClientSpecificInterfacedMiddleware : IProducerMiddleware<RabbitProducerProperties>
    {
        private readonly ITestCaller testCaller;

        public ClientSpecificInterfacedMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task InvokeAsync(
            ProducerContext<RabbitProducerProperties> context,
            ProducerDelegate<RabbitProducerProperties> next,
            CancellationToken cancellationToken)
        {
            testCaller.Call(nameof(ClientSpecificInterfacedMiddleware));
            return next(context, cancellationToken);
        }
    }

    [Fact]
    public async Task IncludeSpecificInterfaceMiddleware()
    {
        fixture.ServiceCollection.AddScoped<ClientSpecificInterfacedMiddleware>();
        using var producer = fixture.GetProducerBuilder()
            .WithMiddleware<ClientSpecificInterfacedMiddleware>()
            .BuildRabbit<string>(new RabbitProducerParameters("test"));
        await producer.Send("whatever", "test message", CancellationToken.None);

        caller.Verify(c => c.Call("ClientSpecificInterfacedMiddleware"), Times.Once);
        caller.VerifyNoOtherCalls();
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

    [Fact]
    public async Task IncludeClientAgnosticConventionalMiddleware()
    {
        using var producer = fixture.GetProducerBuilder()
            .WithMiddleware<ClientAgnosticConventionalMiddleware>()
            .BuildRabbit<string>(new RabbitProducerParameters("test"));
        await producer.Send("whatever", "test message", CancellationToken.None);

        caller.Verify(c => c.Call("ClientAgnosticConventionalMiddleware"), Times.Once);
        caller.VerifyNoOtherCalls();
    }

    private class ClientSpecificConventionalMiddleware
    {
        private readonly ProducerDelegate<RabbitProducerProperties> next;

        public ClientSpecificConventionalMiddleware(
            ProducerDelegate<RabbitProducerProperties> next)
        {
            this.next = next;
        }

        public Task InvokeAsync(
            ProducerContext<RabbitProducerProperties> context,
            ITestCaller testCaller,
            CancellationToken cancellationToken)
        {
            context.NativeProperties.BasicProperties.Persistent = true;

            testCaller.Call(nameof(ClientSpecificConventionalMiddleware));
            return next(context, cancellationToken);
        }
    }

    [Fact]
    public async Task IncludeSpecificConventionalMiddleware()
    {
        using var producer = fixture.GetProducerBuilder()
            .WithMiddleware<ClientSpecificConventionalMiddleware>()
            .BuildRabbit<string>(new RabbitProducerParameters("test"));
        await producer.Send("whatever", "test message", CancellationToken.None);

        caller.Verify(c => c.Call("ClientSpecificConventionalMiddleware"), Times.Once);
        caller.VerifyNoOtherCalls();
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

    [Fact]
    public async Task NotIncludeSpecificConventionalMiddleware_WhenTypesNotMatch()
    {
        fixture.ServiceCollection.AddScoped<ClientSpecificWrongInterfacedMiddleware>();
        using var producer = fixture.GetProducerBuilder()
            .WithMiddleware<ClientSpecificWrongInterfacedMiddleware>()
            .BuildRabbit<string>(new RabbitProducerParameters("test"));
        await producer.Send("whatever", "test message", CancellationToken.None);

        caller.VerifyNoOtherCalls();
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

    [Fact]
    public async Task IncludeGenericClientSpecificConventionalMiddleware()
    {
        using var producer = fixture.GetProducerBuilder()
            .WithMiddleware(typeof(GenericClientSpecificConventionalMiddleware<>))
            .BuildRabbit<string>(new RabbitProducerParameters("test"));
        await producer.Send("whatever", "test message", CancellationToken.None);

        caller.Verify(c => c.Call("GenericClientSpecificConventionalMiddleware with RabbitProducerProperties"), Times.Once);
        caller.VerifyNoOtherCalls();
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

    [Fact]
    public async Task IncludeClientSpecificConventionalMiddleware_WithGenericEntryPoint()
    {
        using var producer = fixture.GetProducerBuilder()
            .WithMiddleware<GenericClientSpecificConventionalMiddleware>()
            .BuildRabbit<string>(new RabbitProducerParameters("test"));
        await producer.Send("whatever", "test message", CancellationToken.None);

        caller.Verify(c => c.Call("GenericClientSpecificConventionalMiddleware"), Times.Once);
        caller.VerifyNoOtherCalls();
    }
}