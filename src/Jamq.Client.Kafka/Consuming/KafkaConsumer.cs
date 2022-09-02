using Confluent.Kafka;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.Extensions.DependencyInjection;

namespace Jamq.Client.Kafka.Consuming;

public class KafkaConsumer<TKey, TMessage, TProcessor> : IConsumer
    where TProcessor : IProcessor<TKey, TMessage>
{
    private readonly IServiceProvider serviceProvider;
    private readonly KafkaConsumerParameters parameters;
    private readonly Lazy<IConsumer<TKey, TMessage>> nativeConsumer;
    private readonly ConsumerDelegate<TKey, TMessage, ConsumeResult<TKey, TMessage>> pipeline;

    private bool running;
    private bool idle;

    public KafkaConsumer(
        IServiceProvider serviceProvider,
        KafkaConsumerParameters parameters,
        IEnumerable<Func<ConsumerDelegate<TKey, TMessage, ConsumeResult<TKey, TMessage>>, ConsumerDelegate<TKey, TMessage, ConsumeResult<TKey, TMessage>>>> middlewares)
    {
        this.serviceProvider = serviceProvider;
        this.parameters = parameters;
        nativeConsumer = new(
            () => new ConsumerBuilder<TKey, TMessage>(parameters.ConsumerConfig).Build(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        pipeline = middlewares.Reverse().Aggregate(
            (ConsumerDelegate<TKey, TMessage, ConsumeResult<TKey, TMessage>>)((context, cancellationToken) =>
            {
                var processor = context.ServiceProvider.GetRequiredService<TProcessor>();
                return processor.Process(
                    context.Key ?? context.NativeProperties.Message.Key,
                    context.Message ?? context.NativeProperties.Message.Value,
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
        running = true;
        Task.Run(ConsumeLoop);
    }

    private async Task ConsumeLoop()
    {
        while (running)
        {
            var consumeResult = nativeConsumer.Value.Consume(parameters.ConsumeTimeout);
            if (consumeResult is null || consumeResult.IsPartitionEOF)
            {
                idle = true;
                await Task.Delay(parameters.IdleInterval);
                continue;
            }

            idle = false;
            await using var scope = serviceProvider.CreateAsyncScope();
            using var cancellationTokenSource = new CancellationTokenSource();
            var context = new ConsumerContext<TKey, TMessage, ConsumeResult<TKey, TMessage>>(
                scope.ServiceProvider, consumeResult)
            {
                Key = consumeResult.Message.Key,
                Message = consumeResult.Message.Value
            };
            var processResult = await pipeline.Invoke(context, cancellationTokenSource.Token);

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

    public void Cancel() => running = false;

    public bool IsIdle() => idle;

    public void Dispose() => Cancel();
}