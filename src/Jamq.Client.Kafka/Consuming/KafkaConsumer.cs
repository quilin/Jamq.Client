using Confluent.Kafka;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Jamq.Client.Kafka.Consuming;

public class KafkaConsumer<TKey, TMessage, TProcessor> : IConsumer
    where TProcessor : IProcessor<TKey, TMessage>
{
    private readonly IServiceProvider serviceProvider;
    private readonly KafkaConsumerParameters parameters;
    private readonly Lazy<IConsumer<TKey, TMessage>> nativeConsumer;
    private readonly ConsumerDelegate<TKey, TMessage, KafkaConsumerProperties<TKey, TMessage>> pipeline;

    private bool running;
    private bool idle;

    public KafkaConsumer(IServiceProvider serviceProvider,
        KafkaConsumerParameters parameters,
        IEnumerable<Func<
            ConsumerDelegate<TKey, TMessage, KafkaConsumerProperties<TKey, TMessage>>,
            ConsumerDelegate<TKey, TMessage, KafkaConsumerProperties<TKey, TMessage>>>> middlewares,
        Func<ConsumerBuilder<TKey, TMessage>, ConsumerBuilder<TKey, TMessage>> enrichBuilder)
    {
        this.serviceProvider = serviceProvider;
        this.parameters = parameters;
        nativeConsumer = new(
            () => enrichBuilder.Invoke(new ConsumerBuilder<TKey, TMessage>(parameters.ConsumerConfig)).Build(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        pipeline = middlewares.Reverse().Aggregate(
            (ConsumerDelegate<TKey, TMessage, KafkaConsumerProperties<TKey, TMessage>>)((context, cancellationToken) =>
            {
                var processor = context.ServiceProvider.GetRequiredService<TProcessor>();
                return processor.Process(
                    context.Key ?? context.NativeProperties.ConsumeResult.Message.Key,
                    context.Message ?? context.NativeProperties.ConsumeResult.Message.Value,
                    cancellationToken);
            }),
            (current, component) => component(current));
    }

    public void Subscribe()
    {
        if (running)
        {
            return;
        }

        nativeConsumer.Value.Subscribe(parameters.Topic);
        Event.WriteIfEnabled(CommonDiagnostics.ConsumerSubscribed, new { parameters.Topic });
        running = true;
        Task.Run(ConsumeLoop).ConfigureAwait(false);
    }

    private async Task ConsumeLoop()
    {
        while (running)
        {
            var consumeResult = nativeConsumer.Value.Consume(parameters.ConsumeTimeout);
            if (consumeResult is null || consumeResult.IsPartitionEOF)
            {
                idle = true;
                Event.WriteIfEnabled(KafkaDiagnostics.TopicConsumed, new { parameters.Topic });
                await Task.Delay(parameters.IdleInterval).ConfigureAwait(false);
                continue;
            }

            idle = false;
            Event.WriteIfEnabled(
                CommonDiagnostics.MessageReceived,
                new { parameters.Topic, consumeResult.Message.Key });
            await using var scope = serviceProvider.CreateAsyncScope();
            using var cancellationTokenSource = new CancellationTokenSource();
            var properties = new KafkaConsumerProperties<TKey, TMessage>(consumeResult, parameters);
            var context = new ConsumerContext<TKey, TMessage, KafkaConsumerProperties<TKey, TMessage>>(
                scope.ServiceProvider, properties)
            {
                Key = consumeResult.Message.Key,
                Message = consumeResult.Message.Value
            };

            ProcessResult processResult;
            try
            {
                processResult = await pipeline.Invoke(context, cancellationTokenSource.Token).ConfigureAwait(false);
                Event.WriteIfEnabled(
                    CommonDiagnostics.MessageProcessSuccess,
                    new { parameters.Topic, consumeResult.Message.Key });
            }
            catch (Exception e)
            {
                processResult = ProcessResult.Failure;
                Event.WriteIfEnabled(
                    CommonDiagnostics.MessageProcessFailure,
                    new { parameters.Topic, Exception = e, consumeResult.Message.Key });
            }
            finally
            {
                Event.WriteIfEnabled(
                    CommonDiagnostics.MessageProcessComplete,
                    new { parameters.Topic, consumeResult.Message.Key });
            }

            switch (processResult)
            {
                case ProcessResult.Success:
                case ProcessResult.Failure:
                    nativeConsumer.Value.Commit(consumeResult);
                    break;
                case ProcessResult.RetryNeeded:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public void Cancel()
    {
        if (nativeConsumer is { IsValueCreated: true })
        {
            var consumer = nativeConsumer.Value;
            consumer.Close();
            consumer.Dispose();
        }

        Event.WriteIfEnabled(CommonDiagnostics.ConsumerUnsubscribed, new { parameters.Topic });
        running = false;
    }

    public bool IsIdle() => idle;

    public void Dispose() => Cancel();
}