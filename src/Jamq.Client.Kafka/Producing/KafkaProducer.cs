using Confluent.Kafka;
using Jamq.Client.Abstractions.Producing;
using Microsoft.Extensions.DependencyInjection;

namespace Jamq.Client.Kafka.Producing;

public class KafkaProducer<TKey, TMessage> : Abstractions.Producing.IProducer<TKey, TMessage>
{
    private readonly IServiceProvider serviceProvider;
    private readonly KafkaProducerParameters parameters;
    private readonly ProducerDelegate<TKey, TMessage, Message<TKey, TMessage>> pipeline;
    private readonly Lazy<Confluent.Kafka.IProducer<TKey, TMessage>> nativeProducer;

    public KafkaProducer(
        IServiceProvider serviceProvider,
        KafkaProducerParameters parameters,
        IEnumerable<Func<ProducerDelegate<TKey, TMessage, Message<TKey, TMessage>>, ProducerDelegate<TKey, TMessage, Message<TKey, TMessage>>>> middlewares)
    {
        this.serviceProvider = serviceProvider;
        this.parameters = parameters;
        pipeline = middlewares.Reverse().Aggregate(
            (ProducerDelegate<TKey, TMessage, Message<TKey, TMessage>>)SendMessage,
            (current, component) => component(current));
        nativeProducer = new(
            () => new ProducerBuilder<TKey, TMessage>(parameters.ProducerConfig).Build(),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task Send(TKey key, TMessage message, CancellationToken cancellationToken)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        await using var scope = serviceProvider.CreateAsyncScope();

        var nativeMessage = new Message<TKey, TMessage> { Key = key, Value = message };
        var context = new ProducerContext<TKey, TMessage, Message<TKey, TMessage>>(
            scope.ServiceProvider, nativeMessage, key, message);
        await pipeline.Invoke(context, cancellationToken);
    }

    private Task SendMessage(
        ProducerContext<TKey, TMessage, Message<TKey, TMessage>> context,
        CancellationToken cancellationToken) =>
        nativeProducer.Value.ProduceAsync(parameters.Topic, context.NativeProperties, cancellationToken);

    public void Dispose()
    {
        if (nativeProducer is { IsValueCreated: true })
        {
            nativeProducer.Value.Dispose();
        }
    }
}