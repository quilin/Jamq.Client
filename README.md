# RMQ.Client

## Reasoning

This wrapper has been written as a high level wrapper over standard RabbitMQ.Client package. The reasoning behind it is that every time I came into a new company, same thing has to be done over and over again - add some encoding/decoding, enrich messages with OpenTelemetry (or more usual - custom Correlation token implementation), do some DI stuff etc. Same goes for using Kafka. Of course, one could use MassTransit, but it brings the terrible topology and contract conventions with it that in order to support the whole "forget about transport" mindset.

## When use it?

 - When you need to go with a quick start with RabbitMQ without thinking of connections, channels, robustness, encoding and decoding and etc.
 - When you need easy and familiar way of managing the pipelines of producers and consumers like in usual dotnet core applications.
 - When you need to be able to unit test your RabbitMQ interaction
 
## How to use it?

NOTE: NuGet packages are yet to come

First you need to install the according packages:
 - `RMQ.Client.Abstractions` contains all needed contracts and interfaces to use in your code
 - `RMQ.Client.DependencyInjection` contains all needed extensions for your `IServiceCollection` for proper dependency registration
 - `RMQ.Client` is the implementation package and it is required for the code to work in your project.

In common architecture you would have the main endpoint project and some domain logic projects separated from each other. In this case you might want to use `RMQ.Client.Abstractions` package for your domain projects and add the `RMQ.Client` and `RMQ.Client.DependencyInjection` to the API (endpoint) project.

Now you can use the `ConfigureServices` method of your `Startup.cs` to register the dependencies:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddRmqClient(new RabbitConnectionParameters());
}
```

This is enough for a quickstart with connection to default `amqp://localhost:5672/` RabbitMQ endpoint as `guest:guest`. If you want to use specific endpoint, you need another constructor:

```c#
new RabbitConnectionParameters("amqp://my-rabbit-mq-host:5673", "admin", "secret");
```

#### TODOS
 - `IOptions<RabbitConnectionParameters>` for dynamic configuration registration
 - Registration of generic scoped consumers to inject `RabbitConsumer<THandler, TMessage>` without builder

### Producer

Now that you registered the required dependencies, it's time to publish some messages:

```csharp
public class InternalService
{
    private readonly IProducer producer;

    public InternalService(IProducerBuilder producerBuilder)
    {
        var parameters = new RabbitProducerParameters("example-exchange");
        var producer = producerBuilder.BuildRabbit(parameters);
    }

    public async Task DoIt(CancellationToken cancellationToken)
    {
        var message = new DomainModel
        {
            Id = Guid.NewGuid(),
            Number = 1255,
            Text = "Hello, world!"
        };
        await this.producer.Send("routing-key", message, cancellationToken);
    }
}
```

Producer is bind by configuration to the exchange, and creating the producer will ensure that the exchange will actually exist.

By default the client is using `System.Text.Json` to encode the message with basic Web settings. Try not to put circular references in those sweet messages of yours.

If you want to change the encoding implementation, you need to modify the producing pipeline. It can be done in several ways, but it all comes up to removing default producing middlewares and adding your own. Removing defaults is being done by `Flush()` builder method

To add the middleware you may use three approaches:

1. Middleware implementing the interface `IProducerMiddleware`
```csharp
public InternalService(IProducerBuilder producerBuilder)
{
    var parameters = new RabbitProducerParameters("example-exchange");
    var producer = producerBuilder.Flush() // remove default middlewares
        .WithMiddleware<CustomEncodingMiddleware>()
        .BuildRabbit(parameters);
}

public class CustomEncodingMiddleware : IProducerMiddleware
{
    public async Task InvokeAsync(
        ProducerContext context,
        ProducerDelegate next)
    {
        context.Body = new byte[10]; // or a smarter way of encoding
        return next(context);
    }
}
```

Middleware, being the usual dependency itself, should be registered. Thus, you may inject any proper dependencies in its constructor as well.

The middleware will be resolved in a new scope each time you call `Send` method. If, for some reason you do not want to use the interface, you may register...

2. The duck-typed middleware:

```csharp
public class CustomConventionalMiddleware
{
    private readonly ProduceDelegate next;

    public CustomConventionalMiddleware(ProduceDelegate next)
    {
        this.next = next;
    }

    public Task InvokeAsync(
        ProducerContext context,
        ILogger<CustomConventionalMiddleware> logger)
    {
        logger.LogDebug("Sending the message");
        return this.next(context);
    }
}
```

When you don't feel like creating the class and stuff, you may just go with...

3. The lambda middleware

```csharp
public InternalService(IProducerBuilder producerBuilder)
{
    var parameters = new RabbitProducerParameters("example-exchange");
    var producer = producerBuilder.Flush() // remove default middlewares
        .With(next => async context => {
            var logger = context.ServiceProvider.GetService<ILogger>();
            logger.LogDebug("Sending the message");
            return await next(context);
        })
        .BuildRabbit(parameters);
}
```

Middlewares can be chained and will preserve the order they were added on sending the message.

If you don't want to bother flushing and adding middlewares each time you create the producer, you need to use the registration feature:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddRmqClient(
        new RabbitConnectionParameters(),
        producerBuilder => producerBuilder.Flush()
            .WithMiddleware<CustomEncodingMiddleware>()
            .WithMiddleware<CustomConventionalMiddleware>());
}
```

Now every time you inject the `IProducerBuilder` it will come from this enrichment factory.

#### Client-agnostic and client-aware producer middleware

By default, all producer middlewares are client agnostic, which means But what if you want to interact with the underlying client directly from the middleware? For example to put the correlation token into the message headers. In this case you might want to create the client-aware middleware:
```csharp
public class CorrelationEnrichmentMiddleware : IProducerMiddleware<IBasicProperties>
{
    private readonly ICorrelationTokenProvider correlationTokenProvider;

    public CorrelationEnrichmentMiddleware(ICorrelationTokenProvier correlationTokenProvider)
    {
        this.correlationTokenProvider = correlationTokenProvider;
    }
    
    public async Task InvokeAsync(
        ProducerContext<IBasicProperties> context,
        ProducerDelegate<IBasicProperties> next)
    {
        context.NativeProperties.CorrelationId = correlationTokenProvider.Current;
        await next(context);
    }
}
```

When you add such a middleware to the builder, you do not expect it to be a part of a pipeline for other clients. But what if you want your middleware to be client-aware and generic at the same time?

```csharp
public class GenericCorrelationEnrichmentMiddleware<TNativeProperties>
    : IProducerMiddleware<TNativeProperties>
{
    private readonly ICorrelationTokenProvider correlationTokenProvider;

    public CorrelationEnrichmentMiddleware(ICorrelationTokenProvier correlationTokenProvider)
    {
        this.correlationTokenProvider = correlationTokenProvider;
    }
    
    public async Task InvokeAsync(
        ProducerContext<TNativeProperties> context,
        ProducerDelegate<TNativeProperties> next)
    {
        if (typeof(TNativeProperties).IsAssignableFrom(typeof(IBasicProperties))
        {
            context.NativeProperties.CorrelationId = correlationTokenProvider.Current;
        }

        await next(context);
    }
}
```

In this case it would be impossible to add the middleware to pipeline with generic `WithMiddleware<>` method and you will have to use

```csharp
producerBuilder.WithMiddleware(typeof(GenericCorrelationEnrichmentMiddleware<>))
```

Open generic types will be automatically instantiated with certain `TNativeProperties` type declaration on `.BuildRabbit()`.

#### TODOs
1. Cancellation token for middlewares
2. Generic producer context to contain the typed message
