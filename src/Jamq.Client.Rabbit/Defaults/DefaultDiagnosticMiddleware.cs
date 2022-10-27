using System.Diagnostics;
using System.Text;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Diagnostics;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Consuming;
using Jamq.Client.Rabbit.Producing;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.Defaults;

public abstract class DefaultDiagnosticMiddleware
{
    protected static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
}

public class DefaultDiagnosticMiddleware<TMessage> : DefaultDiagnosticMiddleware,
    IProducerMiddleware<string, TMessage, RabbitProducerProperties>,
    IConsumerMiddleware<string, TMessage, RabbitConsumerProperties>
{
    public async Task InvokeAsync(
        ProducerContext<string, TMessage, RabbitProducerProperties> context,
        ProducerDelegate<string, TMessage, RabbitProducerProperties> next,
        CancellationToken cancellationToken)
    {
        var exchangeName = context.NativeProperties.Parameters.ExchangeName;
        using var activity = Event.ActivitySource.StartActivity(
            Event.Produce(exchangeName),
            ActivityKind.Producer,
            default(ActivityContext));
        var activityContext = activity?.Context ?? Activity.Current?.Context ?? default;

        activity?.AddTag("messaging.system", "rabbitmq");
        activity?.AddTag("messaging.destination_kind", "topic");
        activity?.AddTag("messaging.destination", context.NativeProperties.Parameters.ExchangeName);
        activity?.AddTag("messaging.rabbitmq.routing_key", context.Key);

        Propagator.Inject(
            new PropagationContext(activityContext, Baggage.Current),
            context.NativeProperties.BasicProperties,
            Inject);

        await next.Invoke(context, cancellationToken).ConfigureAwait(false);
    }

    private static void Inject(IBasicProperties target, string key, string value)
    {
        target.Headers ??= new Dictionary<string, object>();
        target.Headers[key] = value;
    }

    public async Task<ProcessResult> InvokeAsync(
        ConsumerContext<string, TMessage, RabbitConsumerProperties> context,
        ConsumerDelegate<string, TMessage, RabbitConsumerProperties> next,
        CancellationToken cancellationToken)
    {
        var basicDeliverEventArgs = context.NativeProperties.BasicDeliverEventArgs;

        var parentContext = Propagator.Extract(default, basicDeliverEventArgs.BasicProperties, Extract);
        Baggage.Current = parentContext.Baggage;

        using var activity = Event.ActivitySource.StartActivity(
            Event.Consume(context.NativeProperties.Parameters.QueueName, basicDeliverEventArgs.ConsumerTag),
            ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.AddTag("messaging.system", "rabbitmq");
        activity?.AddTag("messaging.destination_kind", "queue");
        activity?.AddTag("messaging.destination", context.NativeProperties.Parameters.DeclaredQueueName);
        activity?.AddTag("messaging.message_id", basicDeliverEventArgs.DeliveryTag);
        activity?.AddTag("messaging.conversation_id", basicDeliverEventArgs.BasicProperties.CorrelationId);
        activity?.AddTag("messaging.consumer_id", basicDeliverEventArgs.ConsumerTag);
        activity?.AddTag("messaging.rabbitmq.routing_key", context.Key);

        return await next.Invoke(context, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> Extract(IBasicProperties source, string key) =>
        source.Headers?.TryGetValue(key, out var value) is true
            ? new[] {Encoding.UTF8.GetString(value as byte[] ?? Array.Empty<byte>())}
            : Enumerable.Empty<string>();
}