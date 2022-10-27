using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Diagnostics;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Kafka.Consuming;
using Jamq.Client.Kafka.Producing;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Jamq.Client.Kafka.Defaults;

public abstract class DefaultDiagnosticMiddleware
{
    protected static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
}

public class DefaultDiagnosticMiddleware<TKey, TMessage> : DefaultDiagnosticMiddleware,
    IProducerMiddleware<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>>,
    IConsumerMiddleware<TKey, TMessage, KafkaConsumerProperties<TKey, TMessage>>
{
    public async Task InvokeAsync(
        ProducerContext<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>> context,
        ProducerDelegate<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>> next,
        CancellationToken cancellationToken)
    {
        using var activity = Event.ActivitySource.StartActivity(
            Event.Produce(context.NativeProperties.Parameters.Topic),
            ActivityKind.Producer,
            default(ActivityContext));
        var activityContext = activity?.Context ?? Activity.Current?.Context ?? default;

        activity?.AddTag("messaging.system", "kafka");
        activity?.AddTag("messaging.destination_kind", "topic");
        activity?.AddTag("messaging.destination", context.NativeProperties.Parameters.Topic);

        Propagator.Inject(
            new PropagationContext(activityContext, Baggage.Current),
            context.NativeProperties.Message,
            Inject);

        await next.Invoke(context, cancellationToken).ConfigureAwait(false);
    }

    private static void Inject(Message<TKey, TMessage> target, string key, string value)
    {
        target.Headers ??= new Headers();
        target.Headers.Add(key, Encoding.UTF8.GetBytes(value));
    }

    public async Task<ProcessResult> InvokeAsync(
        ConsumerContext<TKey, TMessage, KafkaConsumerProperties<TKey, TMessage>> context,
        ConsumerDelegate<TKey, TMessage, KafkaConsumerProperties<TKey, TMessage>> next,
        CancellationToken cancellationToken)
    {
        var parentContext = Propagator.Extract(
            default,
            context.NativeProperties.ConsumeResult.Message.Headers,
            Extract);
        Baggage.Current = parentContext.Baggage;

        var topic = context.NativeProperties.Parameters.Topic;
        var groupId = context.NativeProperties.Parameters.ConsumerConfig.GroupId;

        using var activity = Event.ActivitySource.StartActivity(
            Event.Consume(topic, groupId),
            ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.AddTag("messaging.system", "kafka");
        activity?.AddTag("messaging.destination_kind", "topic");
        activity?.AddTag("messaging.destination", topic);
        activity?.AddTag("messaging.kafka.consumer_group", groupId);
        activity?.AddTag("messaging.kafka.partition", context.NativeProperties.ConsumeResult.Partition);

        return await next.Invoke(context, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> Extract(Headers source, string key) =>
        source.TryGetLastBytes(key, out var headerBytes)
            ? new[] { Encoding.UTF8.GetString(headerBytes ?? Array.Empty<byte>()) }
            : Enumerable.Empty<string>();
}