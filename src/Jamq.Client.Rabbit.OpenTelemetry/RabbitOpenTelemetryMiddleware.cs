using System.Diagnostics;
using System.Text;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Consuming;
using Jamq.Client.Rabbit.Producing;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.OpenTelemetry;

public class RabbitOpenTelemetryMiddleware<TMessage> :
    IProducerMiddleware<string, TMessage, RabbitProducerProperties>,
    IConsumerMiddleware<RabbitConsumerProperties>
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private static readonly ActivitySource ActivitySource = new(nameof(RabbitOpenTelemetryMiddleware<TMessage>));

    public async Task InvokeAsync(
        ProducerContext<string, TMessage, RabbitProducerProperties> context,
        ProducerDelegate<string, TMessage, RabbitProducerProperties> next,
        CancellationToken cancellationToken)
    {
        var exchangeName = context.NativeProperties.ProducerParameters.ExchangeName;
        var activityName = $"producer-{exchangeName}";

        using var activity = ActivitySource.StartActivity(activityName, ActivityKind.Producer);
        var contextToInject = activity switch
        {
            null => Activity.Current?.Context ?? default(ActivityContext),
            _ => activity.Context
        };
        
        Propagator.Inject(
            new PropagationContext(contextToInject, Baggage.Current),
            context.NativeProperties.BasicProperties,
            Inject);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.destination", exchangeName);
        activity?.SetTag("messaging.rabbitmq.routing_key", context.Key);

        await next.Invoke(context, cancellationToken);
    }

    public async Task<ProcessResult> InvokeAsync(
        ConsumerContext<RabbitConsumerProperties> context,
        ConsumerDelegate<RabbitConsumerProperties> next,
        CancellationToken cancellationToken)
    {
        var basicProperties = context.NativeProperties.BasicDeliverEventArgs.BasicProperties;
        var consumerParameters = context.NativeProperties.ConsumerParameters;

        var parentContext = Propagator.Extract(default, basicProperties, Extract);
        Baggage.Current = parentContext.Baggage;

        var activityName = $"consumer-{consumerParameters.QueueName}-{consumerParameters.ConsumerTag}";
        using var activity = ActivitySource.StartActivity(
            activityName,
            ActivityKind.Consumer,
            parentContext.ActivityContext);
        return await next.Invoke(context, cancellationToken);
    }

    private static IEnumerable<string> Extract(IBasicProperties basicProperties, string key) =>
        basicProperties.Headers.TryGetValue(key, out var header)
            ? new[] { Encoding.UTF8.GetString(header as byte[] ?? Array.Empty<byte>()) }
            : Enumerable.Empty<string>();

    private static void Inject(IBasicProperties basicProperties, string key, string value) => 
        basicProperties.Headers[key] = value;
}