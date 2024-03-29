﻿using Confluent.Kafka;
using Jamq.Client.Abstractions.Diagnostics;
using Jamq.Client.Abstractions.Producing;
using Microsoft.Extensions.DependencyInjection;

namespace Jamq.Client.Kafka.Producing;

public class KafkaProducer<TKey, TMessage> : Abstractions.Producing.IProducer<TKey, TMessage>
{
    private readonly IServiceProvider serviceProvider;
    private readonly KafkaProducerParameters parameters;
    private readonly ProducerDelegate<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>> pipeline;
    private readonly Lazy<Confluent.Kafka.IProducer<TKey, TMessage>> nativeProducer;

    public KafkaProducer(IServiceProvider serviceProvider,
        KafkaProducerParameters parameters,
        IEnumerable<Func<
            ProducerDelegate<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>>,
            ProducerDelegate<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>>>> middlewares,
        Func<ProducerBuilder<TKey, TMessage>, ProducerBuilder<TKey, TMessage>> enrichBuilder)
    {
        this.serviceProvider = serviceProvider;
        this.parameters = parameters;
        pipeline = middlewares.Reverse().Aggregate(
            (ProducerDelegate<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>>)SendMessage,
            (current, component) => component(current));
        nativeProducer = new(
            () => enrichBuilder.Invoke(new ProducerBuilder<TKey, TMessage>(parameters.ProducerConfig)).Build(),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task Send(TKey key, TMessage message, CancellationToken cancellationToken)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        await using var scope = serviceProvider.CreateAsyncScope();

        var nativeMessage = new Message<TKey, TMessage> { Key = key, Value = message };
        var properties = new KafkaProducerProperties<TKey, TMessage>(nativeMessage, parameters);
        var context = new ProducerContext<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>>(
            scope.ServiceProvider, properties, key, message);
        await pipeline.Invoke(context, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendMessage(
        ProducerContext<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>> context,
        CancellationToken cancellationToken)
    {
        await nativeProducer.Value
            .ProduceAsync(parameters.Topic, context.NativeProperties.Message, cancellationToken)
            .ConfigureAwait(false);
        Event.WriteIfEnabled(CommonDiagnostics.MessagePublished, new { parameters.Topic, context.Key });
    }

    public void Dispose()
    {
        if (nativeProducer is { IsValueCreated: true })
        {
            nativeProducer.Value.Dispose();
        }
    }
}