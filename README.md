# Jamq.Client

Just another message queueing client :)

## Reasoning

This library has started as a high level wrapper over standard RabbitMQ.Client package. The reasoning behind it is that every time I came into a new company, same thing has to be done over and over again - add some encoding/decoding, enrich messages with OpenTelemetry (or more usual - custom Correlation token implementation), do some DI stuff etc. Same goes for using Kafka. Of course, one could use MassTransit, but it brings the terrible topology and contract conventions with it that in order to support the whole "forget about transport" mindset. In case you need something in-between the librdkafka and full-scale framework, you will find yourself in a deficite situation.

## When use it?

- When you need to go with a quick start with RabbitMQ/Kafka without thinking of connections, channels, robustness, encoding and decoding etc
- When you need RabbitMQ/Kafka library and not MessageBus framework
- When you need easy and familiar way of managing the pipelines of producers and consumers like in usual dotnet core applications
- When you need to be able to unit test your MQ interaction

## How to use it?

First you need to install the according packages from NuGet:
- `Jamq.Client.Abstractions` contains all abstract contracts and interfaces to use in your code
- `Jamq.Client` contains the implementations for the abstractions above
- `Jamq.Client.DependencyInjection` contains basic extensions for your `IServiceCollection` for proper dependency registration
- `Jamq.Client.Rabbit` and `Jamq.Client.Rabbit.DependencyInjection` contains the contracts and implementations to register and work with RabbitMQ
- `Jamq.Client.Kafka` and `Jamq.Client.Kafka.DependencyInjection` - same for Kafka

In common architecture you would have the main endpoint project and some domain logic projects separated from each other. In this case you might want to use `Jamq.Client.Abstractions` package for your domain projects and add the `Jamq.Client` and `Jamq.Client.DependencyInjection` to the API (endpoint) project.

Now you can use the `ConfigureServices` method of your `Startup.cs` to register the dependencies:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddJamqClient(config => config
        .UseRabbit(new RabbitConnectionParameters()));
}
```

Once you registered the client, you should be able to inject `IProducerBuilder`s and `IConsumerBuilder`s in your units.

### Pipelines and middlewares

By default the producers and consumers are enriched with some generic encoding/decoding. Every builder instance is stateful and contains the list of registered middlewares that may add more enrichment of your design. But adding it manually to every producer/consumer builder that you have will be terrible - that is why you can register some defaults for producer/consumer builders in same `.AddJamqClient` method:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddJamqClient(
        config => config.UseRabbit(new RabbitConnectionParameters()),
        producerBuilder => producerBuilder.WithMiddleware<ProducerCorrelationMiddleware>(),
        consumerBuilder => consumerBuilder.WithMiddleware<ConsumerCorrelationMiddleware>());
}
```

Now every `I*Builder` that you will inject in your classes will already have the predefined middlewares. Yay!

Although Producers and Consumers implementations may vary, the middlewares are the high-level abstractions and may be applied to Producers and Consumers disregarding the message queue system they are based on. There are several types of middlewares you can use.



## Rabbit MQ

This is enough for a quickstart with connection to default `amqp://localhost:5672/` RabbitMQ endpoint as `guest:guest`. If you want to use specific endpoint, you need another constructor:

```c#
new RabbitConnectionParameters("amqp://my-rabbit-mq-host:5673", "admin", "secret");
```

**TODOS:**
- Generic conventional middlewares (e.g. `class TMw<TKey> { ctor(Delegate<TKey, Message, Props> next) }` - same as interface)
- Registration of generic scoped consumers to inject `RabbitConsumer<THandler, TMessage>` without builder (named `HttpClient`-style)

### Producer

Now that you registered the required dependencies, it's time to publish some messages:

```csharp
public class InternalService
{
    private readonly IProducer producer;

    public InternalService(IProducerBuilder producerBuilder)
    {
        var parameters = new RabbitProducerParameters("example-exchange");
        var producer = producerBuilder.BuildRabbit<DomainModel>(parameters);
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
        ProducerDelegate next,
        CancellationToken token)
    {
        context.Body = new byte[10]; // or a smarter way of encoding
        return next(context, token);
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
        ILogger<CustomConventionalMiddleware> logger,
        CancellationToken token)
    {
        logger.LogDebug("Sending the message");
        return this.next(context, token);
    }
}
```

Notice, that in order for duck-typed middleware to work it needs to receive `ProducerContext` of any sort as the first argument and `CancellationToken` as the last one. Rules and restrictions for the duck-typed middleware are listed in dedicated section to the bottom of this page.

When you don't feel like creating the class and stuff, you may just go with...

3. The lambda middleware

```csharp
public InternalService(IProducerBuilder producerBuilder)
{
    var parameters = new RabbitProducerParameters("example-exchange");
    var producer = producerBuilder.Flush() // remove default middlewares
        .With(next => async (context, token) => {
            var logger = context.ServiceProvider.GetService<ILogger>();
            logger.LogDebug("Sending the message");
            return await next(context, token);
        })
        .BuildRabbit(parameters);
}
```

Middlewares can be chained and will preserve the order they were added on sending the message.

If you don't want to bother flushing and adding middlewares each time you create the producer, you need to use the registration feature:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddJamqClient(
        config => config.UseRabbit(new RabbitConnectionParameters()),
        producerBuilder => producerBuilder.Flush()
            .WithMiddleware<CustomEncodingMiddleware>()
            .WithMiddleware<CustomConventionalMiddleware>());
}
```

Now every time you inject the `IProducerBuilder` it will come from this enrichment factory.

#### Client-agnostic and client-aware producer middleware

By default, all producer middlewares are client agnostic, which means But what if you want to interact with the underlying client directly from the middleware? For example to put the correlation token into the message headers. In this case you might want to create the client-aware middleware:
```csharp
public class CorrelationEnrichmentMiddleware : IProducerMiddleware<RabbitProducerProperties>
{
    private readonly ICorrelationTokenProvider correlationTokenProvider;

    public CorrelationEnrichmentMiddleware(ICorrelationTokenProvier correlationTokenProvider)
    {
        this.correlationTokenProvider = correlationTokenProvider;
    }
    
    public async Task InvokeAsync(
        ProducerContext<RabbitProducerProperties> context,
        ProducerDelegate<RabbitProducerProperties> next,
        CancellationToken token)
    {
        context.NativeProperties.BasicProperties.CorrelationId = correlationTokenProvider.Current;
        await next(context, token);
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
        ProducerDelegate<TNativeProperties> next,
        CancellationToken token)
    {
        if (typeof(TNativeProperties).IsAssignableFrom(typeof(IBasicProperties))
        {
            context.NativeProperties.CorrelationId = correlationTokenProvider.Current;
        }

        await next(context, token);
    }
}
```

In this case it would be impossible to add the middleware to pipeline with generic `WithMiddleware<>` method and you will have to use

```csharp
producerBuilder.WithMiddleware(typeof(GenericCorrelationEnrichmentMiddleware<>))
```

Open generic types will be automatically instantiated with certain `TNativeProperties` type declaration on `.BuildRabbit()`.

### Consumer

Consumers are stateful components that are responsible for receiving and processing incoming messages. "Stateful" means just two states:
- Listening
- Not listening

In order to create the consumer, you need to inject the `IConsumerBuilder`:

```csharp
public InternalService(IConsumerBuilder consumerBuilder)
{
    var consumer = consumerBuilder.BuildRabbit<Message, Processor>(
        new RabbitConsumerParameters("ConsumerTag", "exchange-name", ProcessingOrder.Sequential));
    consumer.Subscribe();
}
```

The processing order parameter not only affects how the consumer will work, but also how the RabbitMQ broker will interact with it (via QoS). By default the consumer uses `ProcessingOrder.Unmanaged` which means that however the queue or broker is set up. In case you want to customize it, there are two ways: `ProcessingOrder.Sequential` and `ProcessingOrder.Parallel.WithMaximumDegree(myNumber)`. The parallelism limitation are not being controlled by the library, but instead by the same QoS policies of the broker. You can (and should) read more about it in the

Notice that in order to create a rabbit consumer you are supposed to declare two types: the message and processor. Message has no constraints, it can be anything, but the processor needs to implement the `IProcessor` interface:

```csharp
public record Message(string Text);

public MessageProcessor : IProcessor<Message>
{
    public MessageProcessor(
        private readonly ILogger<Processor> logger,
        private readonly IMessageStorage storage);

    public async Task<ProcessResult> Process(Message message, CancellationToken token)
    {
        logger.LogInformation($"Incoming message: {message.Text}");
        await storage.Save(message.Text, token);
        return ProcessResult.Success;
    }
}
```

The processor is expected to return the `ProcessResult` which defined the follow-up strategy.

__NOTE__: There's nothing wrong with try/catching the processing, but you need to know that every `IProcessor` call is already wrapped in try/finally and if it throws the unhandled exception, default behaviour is `ProcessResult.Failure` which will cause the message to be rejected in terms of RabbitMQ. So you can keep your code simple if that is the behaviour you need.

__NOTE__: The `IProcessor` implementation you use must be registered. We recommend you registering it as scoped or transient dependency, in that case you can utilize the power of having the separated instances for every incoming message.

By default consumer uses `System.Text.Json` to decode messages, but of course, you can select your own decoding mechanism. For that you will need - you guessed it! - a middleware.

As usual, you can go with one of the three ways: lambdas, interface-based or duck-typed. The cases are equivalent to the producer middleware, but instead of returning the `Task` producer middleware should return same `ProcessResult` as the processor.

There are several ways of writing the consumer middleware, and together they cover almost every challenge you might face.

### Client-agnostic middleware

This package is built with the intention of supporting several different MQ clients, although right now it is only basic `RabbitMQ.Client`. So every `ConsumerContext` (as well as `ProducerContext`) is made generic to support different client-specific properties. For example, the `ProducerContext` for `RabbitProducer` is based on `IBasicProperties`, while the `ConsumerContext` is using `BasicDeliverEventArgs`.

The most trivial example of client-agnostic middleware would be fault-tolerance policy, such as retry mechanism. To write such a middleware one may use the interface:

```csharp
public class ConsumerRetryMiddleware : IConsumerMiddleware
{
    private static readonly RetryPolicy = Policy.Handle<Exception>().RetryAsync(5);

    public Task<ProcessResult> InvokeAsync(
        ConsumerContext context,
        ConsumerDelegate next,
        CancellationToken token) =>
        RetryPolicy.ExecuteAsync(() => next.Invoke(context, token));
}
```

There's no difference what is the client or what type is the incoming message if your purpose is to guard local processing. Of course, if you want to implement retry policies by RabbitMQ (like requeue the message multiple times) this library will not suffice.

### Message-agnostic middleware

In other cases you still don't care about the message type, but it is essential for you to operate with certain type of client-specific properties. For example, when you want to add OpenTelemetry to your consumers. For the sake of example, we will use the duck-typed middleware:

```csharp
public class ConsumerOpenTelemetryMiddleware
{
    private readonly ConsumerDelegate<RabbitConsumerProperties> next;

    public ConsumerOpenTelemetryMiddleware(ConsumerDelegate<RabbitConsumerProperties> next)
    {
        this.next = next;
    }

    public async Task<ProcessResult> InvokeNext(
        ConsumerContext<RabbitConsumerProperties> context,
        CancellationToken token)
    {
        var parentContext = Propagator.Extract(
            default,
            context.NativeProperties.BasicDeliverEventArgs.BasicProperties,
            ExtractTraceContext);
        Baggage.Current = parentContext.Baggage;

        var activityName = $"{context.NativeProperties.BasicDeliverEventArgs.ConsumerTag} incoming message";
        using var activity = ActivitySource.StartActivity(
            activityName,
            ActivityKind.Consumer,
            parentContext.ActivityContext);
        return await next.Invoke(context, token);
    }
    
    private static IEnumerable<string> ExtractTraceContext(IBasicProperties properties, string key) =>
        properties.Headers.TryGetValue(key, out var header)
            ? new[] { Encoding.UTF8.GetString(header as byte[]) }
            : Enumerable.Empty<string>();
}
```

A lot of stuff going on here, and the source code is based on the public example of [OpenTelemetry for .NET](https://github.com/open-telemetry/opentelemetry-dotnet/blob/4310e08afdd3a6d2533836c3d71a6f56cf3148ef/examples/MicroserviceExample/Utils/Messaging/MessageReceiver.cs). Of course, in order to extract the OpenTelemetry properties from `ConsumerContext` you will also need to put it to headers first - by implementing the `ProducerMiddleware` of course.

### Message-generic middleware

Another common scenario - is middleware that is aware of the client, but needs to be generic of the message type. Usually you will need that to implement your own decoding middleware:

```csharp
public class NewtonsoftJsonDecodingMiddleware<TMessage> : IConsumerMiddleware<RabbitConsumerProperties, TMessage>
{
    public Task<ProcessResult> InvokeAsync(
        ConsumerContext<RabbitConsumerProperties, TMessage> context,
        ConsumerDelegate<RabbitConsumerProperties, TMessage> next,
        CancellationToken token)
    {
        var body = context.NativeProperties.BasicDeliverEventArgs.Body.ToArray();
        var utf8Body = Encoding.UTF8.GetString(body);
        var message = JsonConvert.DeserializeObject<TMessage>(utf8Body);
        context.Message = message;
        return next.Invoke(context, token);
    }
}
```

But don't use `Newtonsoft.Json` if you can use `System.Text.Json` and don't forget there is a default decoder based on it in this library.

### Generic middleware

There is another scenario where you may not be interested in client properties, but you need the generic message - validation. We never came with the idea how to remove the client from the equation completely, but it doesn't look so bad?

```csharp
public class ConsumerValidationMiddleware<T, TMessage> : IConsumerMiddleware<T, TMessage>
{
    private readonly IValidator<TMessage> validator;

    public ConsumerValidationMiddleware(IValidator<TMessage> validator)
    {
        this.validator = validator;
    }

    public async Task<ProcessResult> InvokeAsync(
        ConsumerContext<T, TMessage> context,
        ConsumerDelegate<T, TMessage> next,
        CancellationToken token)
    {
        await validator.ValidateAndThrowAsync(context.Message, token);
        return await next.Invoke(context, token);
    }
}
```

Oh boy. IS THERE ANOTHER ONE?!

### Specific middleware

Of course, you can create middleware that uses not only specific client properties, but also a specific message type. We have no idea why you would need that, but there you go:

```csharp
public ConsumerService : BackgroundService
{
    private readonly IConsumerBuilder builder;
    public ConsumerService(IConsumerBuilder builder) { this.builder = builder; }

    public override async ExecuteAsync(CancellationToken token) => builder
        .With<RabbitConsumerProperties, string>(next => (context, ct) => {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<ConsumerService>>();
            logger.LogInformation("Here goes the string message: {Message}", context.Message);
            return next.Invoke(context, ct);
        })
        .BuildRabbit<Processor, string>(new RabbitConsumerParameters(/* ... */))
        .Subscribe();
}
```

