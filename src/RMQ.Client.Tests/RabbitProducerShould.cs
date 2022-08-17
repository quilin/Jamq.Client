using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Producing;

namespace RMQ.Client.Tests;

public class RabbitProducerShould : IClassFixture<RabbitFixture>
{
    private readonly RabbitFixture fixture;
    private readonly Mock<ITestCaller> caller;

    public RabbitProducerShould(
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

    [Fact(Skip = "No integration tests yet")]
    public async Task PublishEncodedMessage()
    {
        var producerBuilder = fixture.GetProducerBuilder();
        using var producer = producerBuilder
            .With(next => context =>
            {
                caller.Object.Call("ClientAgnosticLambdaMiddleware");
                return next(context);
            })
            .With<IBasicProperties>(next => context =>
            {
                caller.Object.Call("ClientSpecificLambdaMiddleware");
                return next(context);
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
    }
    
    private class ClientAgnosticInterfacedMiddleware : IProducerMiddleware
    {
        private readonly ITestCaller testCaller;

        public ClientAgnosticInterfacedMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task InvokeAsync(ProducerContext context, ProducerDelegate next)
        {
            testCaller.Call(nameof(ClientAgnosticInterfacedMiddleware));
            return next(context);
        }
    }
    private class ClientSpecificInterfacedMiddleware : IProducerMiddleware<IBasicProperties>
    {
        private readonly ITestCaller testCaller;

        public ClientSpecificInterfacedMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }

        public Task InvokeAsync(ProducerContext<IBasicProperties> context, ProducerDelegate<IBasicProperties> next)
        {
            testCaller.Call(nameof(ClientSpecificInterfacedMiddleware));
            return next(context);
        }
    }
    private class ClientSpecificWrongInterfacedMiddleware : IProducerMiddleware<string>
    {
        private readonly ITestCaller testCaller;

        public ClientSpecificWrongInterfacedMiddleware(ITestCaller testCaller)
        {
            this.testCaller = testCaller;
        }
        
        public Task InvokeAsync(ProducerContext<string> context, ProducerDelegate<string> next)
        {
            testCaller.Call(nameof(ClientSpecificWrongInterfacedMiddleware));
            return next(context);
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
            ITestCaller testCaller)
        {
            testCaller.Call(nameof(ClientAgnosticConventionalMiddleware));
            return next(context);
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
            ITestCaller testCaller)
        {
            if (context.NativeProperties is not null)
            {
                context.NativeProperties.Persistent = true;
            }

            testCaller.Call(nameof(ClientSpecificConventionalMiddleware));
            return next(context);
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
            ITestCaller testCaller)
        {
            testCaller.Call($"{nameof(GenericClientSpecificConventionalMiddleware<TNativeProperties>)} with {typeof(TNativeProperties).Name}");
            return next(context);
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
            ITestCaller testCaller)
        {
            testCaller.Call(nameof(GenericClientSpecificConventionalMiddleware));
            return next(context);
        }
    }
    
    public interface ITestCaller
    {
        void Call(string message);
    }
}