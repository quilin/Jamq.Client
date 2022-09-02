﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Connection;
using Jamq.Client.Rabbit.Connection.Adapters;

namespace Jamq.Client.Rabbit.Consuming;

internal class RabbitConsumer<TMessage, TProcessor> : IConsumer
    where TProcessor : IProcessor<string, TMessage>
{
    private readonly RabbitConsumerParameters parameters;
    private readonly ILogger? logger;
    private readonly IChannelPool channelPool;
    private readonly IServiceProvider serviceProvider;
    private readonly ConsumerDelegate<string, TMessage, BasicDeliverEventArgs> pipeline;

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
        ILogger? logger,
        IEnumerable<Func<ConsumerDelegate<string, TMessage, BasicDeliverEventArgs>, ConsumerDelegate<string, TMessage, BasicDeliverEventArgs>>> middlewares)
    {
        this.channelPool = channelPool;
        this.serviceProvider = serviceProvider;
        this.parameters = parameters;
        this.logger = logger;

        pipeline = middlewares.Reverse().Aggregate(
            (ConsumerDelegate<string, TMessage, BasicDeliverEventArgs>)((context, cancellationToken) =>
            {
                var processor = context.ServiceProvider.GetRequiredService<TProcessor>();
                return processor.Process(
                    context.Key ?? context.NativeProperties.RoutingKey,
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

        async Task IncomingMessageHandler(object sender, BasicDeliverEventArgs nativeProperties)
        {
            try
            {
                if (currentCancellationTokenSource.IsCancellationRequested)
                {
                    logger?.LogWarning("Consumer stopped working, but the message keep coming");
                    return;
                }

                if (!countdownEvent!.SafeIncrement())
                {
                    logger?.LogWarning("Consumer was not able to increment countdown, returning message to the queue");
                    channelAccessor().BasicNack(nativeProperties.DeliveryTag, false, true);
                    return;
                }

                try
                {
                    ProcessResult processResult;
                    await using (var scope = serviceProvider.CreateAsyncScope())
                    {
                        var context = new ConsumerContext<string, TMessage, BasicDeliverEventArgs>(
                            scope.ServiceProvider, nativeProperties)
                        {
                            Key = nativeProperties.RoutingKey
                        };
                        processResult = await pipeline.Invoke(context, currentCancellationTokenSource.Token);
                    }

                    switch (processResult)
                    {
                        case ProcessResult.Success:
                            channelAccessor().BasicAck(nativeProperties.DeliveryTag, false);
                            break;
                        case ProcessResult.RetryNeeded:
                            channelAccessor().BasicNack(nativeProperties.DeliveryTag, false, true);
                            break;
                        case ProcessResult.Failure:
                            channelAccessor().BasicNack(nativeProperties.DeliveryTag, false, false);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception exception)
                {
                    logger?.LogError(exception, "Consumer message handler has thrown unhandled exception");
                    channelAccessor().BasicNack(nativeProperties.DeliveryTag, false, false);
                }
                finally
                {
                    countdownEvent!.SafeSignal();
                }
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Consumer message handler has thrown unhandled exception");
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
            logger?.LogWarning("Consumer connection to broker was disrupted");
            CloseCurrentConnection(connectionIsDisrupted: true);
            connectionAccessor = CreateConnectionAccessor();

            if (running)
            {
                logger?.LogWarning("Trying to restore connection and resume consuming");
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
            Task.Run(async () => await consumer.OnCancel()).GetAwaiter().GetResult();
            countdownEvent!.Signal();
            cancellationTokenSource!.Cancel();

            if (!countdownEvent.Wait(parameters.MaxProcessingAnticipation))
            {
                logger?.LogError("The consumer didn't wait for message processor to gracefully shutdown and is forcefully stopping it");
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