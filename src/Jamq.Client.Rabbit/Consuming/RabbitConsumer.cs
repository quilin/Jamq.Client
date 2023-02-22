using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Diagnostics;
using Jamq.Client.Rabbit.Connection;
using Jamq.Client.Rabbit.Connection.Adapters;

namespace Jamq.Client.Rabbit.Consuming;

internal class RabbitConsumer<TMessage, TProcessor> : IConsumer
    where TProcessor : IProcessor<string, TMessage>
{
    private readonly RabbitConsumerParameters parameters;
    private readonly IChannelPool channelPool;
    private readonly IServiceProvider serviceProvider;
    private readonly ConsumerDelegate<string, TMessage, RabbitConsumerProperties> pipeline;

    private readonly object sync = new();

    private CountdownEvent? countdownEvent;
    private CancellationTokenSource? cancellationTokenSource;
    private bool running;

    private Lazy<ConsumerConnectionState>? connectionAccessor;
    private readonly Func<IModel> channelAccessor;

    public RabbitConsumer(
        IChannelPool channelPool,
        IServiceProvider serviceProvider,
        RabbitConsumerParameters parameters,
        IEnumerable<Func<ConsumerDelegate<string, TMessage, RabbitConsumerProperties>, ConsumerDelegate<string, TMessage, RabbitConsumerProperties>>> middlewares)
    {
        this.channelPool = channelPool;
        this.serviceProvider = serviceProvider;
        this.parameters = parameters;

        pipeline = middlewares.Reverse().Aggregate(
            (ConsumerDelegate<string, TMessage, RabbitConsumerProperties>)((context, cancellationToken) =>
            {
                var processor = context.ServiceProvider.GetRequiredService<TProcessor>();
                return processor.Process(
                    context.Key ?? context.NativeProperties.BasicDeliverEventArgs.RoutingKey,
                    context.Message!,
                    cancellationToken);
            }),
            (current, component) => component(current));
        channelAccessor = () => connectionAccessor!.Value.ChannelAdapter.Channel;
    }

    public void Subscribe()
    {
        lock (sync)
        {
            if (running)
            {
                return;
            }

            countdownEvent = new CountdownEvent(1);
            cancellationTokenSource = new CancellationTokenSource();

            connectionAccessor = CreateConnectionAccessor();
            parameters.DeclaredQueueName = Ensure.Consume(channelAccessor(), parameters).QueueName;
            Consume();

            running = true;
        }
    }

    private Lazy<ConsumerConnectionState> CreateConnectionAccessor() => new(() =>
    {
        var channelAdapter = channelPool.Get();
        channelAdapter.OnDisrupted += Restore!;

        var channel = channelAdapter.Channel;
        var consumer = new AsyncEventingBasicConsumer(channel);
        var currentCancellationTokenSource = cancellationTokenSource!;

        async Task IncomingMessageHandler(object sender, BasicDeliverEventArgs basicDeliverEventArgs)
        {
            var processDiagnosticInfo = new
            {
                basicDeliverEventArgs.DeliveryTag,
                basicDeliverEventArgs.ConsumerTag,
                parameters.QueueName
            };
            Event.WriteIfEnabled(CommonDiagnostics.MessageReceived, processDiagnosticInfo);
            try
            {
                if (currentCancellationTokenSource.IsCancellationRequested)
                {
                    Event.WriteIfEnabled(RabbitDiagnostics.ConsumerCancelDisrupt, processDiagnosticInfo);
                    return;
                }

                if (!countdownEvent!.SafeIncrement())
                {
                    Event.WriteIfEnabled(RabbitDiagnostics.MessageReceiveDisrupt, processDiagnosticInfo);
                    channelAccessor().BasicNack(basicDeliverEventArgs.DeliveryTag, false, true);
                    return;
                }

                try
                {
                    ProcessResult processResult;
                    await using (var scope = serviceProvider.CreateAsyncScope())
                    {
                        var properties = new RabbitConsumerProperties(basicDeliverEventArgs, parameters);
                        var context = new ConsumerContext<string, TMessage, RabbitConsumerProperties>(
                            scope.ServiceProvider, properties)
                        {
                            Key = basicDeliverEventArgs.RoutingKey
                        };
                        processResult = await pipeline.Invoke(context, currentCancellationTokenSource.Token).ConfigureAwait(false);
                    }

                    switch (processResult)
                    {
                        case ProcessResult.Success:
                            Event.WriteIfEnabled(CommonDiagnostics.MessageProcessSuccess, processDiagnosticInfo);
                            channelAccessor().BasicAck(basicDeliverEventArgs.DeliveryTag, false);
                            break;
                        case ProcessResult.RetryNeeded:
                            Event.WriteIfEnabled(CommonDiagnostics.MessageProcessRetry, processDiagnosticInfo);
                            channelAccessor().BasicNack(basicDeliverEventArgs.DeliveryTag, false, true);
                            break;
                        case ProcessResult.Failure:
                            Event.WriteIfEnabled(CommonDiagnostics.MessageProcessFailure, processDiagnosticInfo);
                            channelAccessor().BasicNack(basicDeliverEventArgs.DeliveryTag, false, false);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception exception)
                {
                    Event.WriteIfEnabled(CommonDiagnostics.MessageProcessFailure, new
                    {
                        basicDeliverEventArgs.DeliveryTag,
                        basicDeliverEventArgs.ConsumerTag,
                        parameters.QueueName,
                        Exception = exception
                    });
                    channelAccessor().BasicNack(basicDeliverEventArgs.DeliveryTag, false, false);
                }
                finally
                {
                    countdownEvent!.SafeSignal();
                }
            }
            catch (Exception exception)
            {
                Event.WriteIfEnabled(RabbitDiagnostics.MessageReceiveDisrupt, new
                {
                    basicDeliverEventArgs.DeliveryTag,
                    basicDeliverEventArgs.ConsumerTag,
                    parameters.QueueName,
                    Exception = exception
                });
            }
        }

        consumer.Received += IncomingMessageHandler;

        var consumerTag = parameters.ConsumerTag.WithRandomSuffix();
        return new ConsumerConnectionState(IncomingMessageHandler, consumer, channelAdapter, consumerTag);
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    private void CloseCurrentConnection(bool connectionIsDisrupted)
    {
        if (connectionAccessor is not { IsValueCreated: true })
        {
            return;
        }

        var connectionState = connectionAccessor.Value;
        connectionState.Consumer.Received -= connectionState.IncomingMessageHandler;
        connectionState.ChannelAdapter.OnDisrupted -= Restore!;

        if (!connectionIsDisrupted)
        {
            connectionState.ChannelAdapter.Channel.BasicCancel(connectionState.ConsumerTag);
        }
        
        connectionState.ChannelAdapter.Dispose();
        connectionAccessor = null;
    }

    private void Restore(object sender, ChannelDisruptedEventArgs e)
    {
        lock (sync)
        {
            CloseCurrentConnection(connectionIsDisrupted: true);
            connectionAccessor = CreateConnectionAccessor();

            if (running)
            {
                Consume();
            }
        }
    }

    private void Consume()
    {
        lock (sync)
        {
            var connectionState = connectionAccessor!.Value;
            channelAccessor().BasicConsume(
                parameters.DeclaredQueueName,
                false,
                connectionState.ConsumerTag,
                connectionState.Consumer);
            running = true;
        }
    }

    public void Cancel()
    {
        lock (sync)
        {
            if (!running || connectionAccessor is not { IsValueCreated: true })
            {
                return;
            }

            var consumer = connectionAccessor.Value.Consumer;
            Task.Run(async () => await consumer.OnCancel()).ConfigureAwait(false).GetAwaiter().GetResult();
            countdownEvent!.Signal();
            cancellationTokenSource!.Cancel();

            if (!countdownEvent.Wait(parameters.MaxProcessingAnticipation))
            {
                Event.WriteIfEnabled(
                    RabbitDiagnostics.ConsumerCancelTimeout,
                    new { parameters.ConsumerTag, parameters.QueueName });
            }

            CloseCurrentConnection(connectionIsDisrupted: false);

            countdownEvent.Dispose();
            cancellationTokenSource.Dispose();
            running = false;
        }
    }

    public bool IsIdle()
    {
        lock (sync)
        {
            if (!running || countdownEvent!.IsSet)
            {
                return true;
            }

            if (countdownEvent.CurrentCount != countdownEvent.InitialCount)
            {
                return false;
            }

            using var channelAdapter = channelPool.Get();
            return channelAdapter.Channel.QueueDeclarePassive(parameters.DeclaredQueueName).MessageCount == 0;
        }
    }

    public void Dispose() => Cancel();
}