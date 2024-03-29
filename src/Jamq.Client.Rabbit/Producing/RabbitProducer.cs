﻿using Jamq.Client.Abstractions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Connection;
using Jamq.Client.Rabbit.Connection.Adapters;

namespace Jamq.Client.Rabbit.Producing;

internal class RabbitProducer<TMessage> : IProducer<string, TMessage>
{
    private readonly IChannelPool channelPool;
    private readonly IServiceProvider serviceProvider;
    private readonly RabbitProducerParameters parameters;
    private readonly ProducerDelegate<string, TMessage, RabbitProducerProperties> pipeline;

    private Lazy<IChannelAdapter> channelAccessor;

    public RabbitProducer(
        IChannelPool channelPool,
        IServiceProvider serviceProvider,
        RabbitProducerParameters parameters,
        IEnumerable<Func<ProducerDelegate<string, TMessage, RabbitProducerProperties>, ProducerDelegate<string, TMessage, RabbitProducerProperties>>> middlewares)
    {
        this.channelPool = channelPool;
        this.serviceProvider = serviceProvider;
        this.parameters = parameters;

        pipeline = middlewares.Reverse().Aggregate((ProducerDelegate<string, TMessage, RabbitProducerProperties>)SendMessage,
            (current, component) => component(current));
        channelAccessor = CreateChannelAccessor(false);
    }

    private Lazy<IChannelAdapter> CreateChannelAccessor(bool restored) => new(() =>
    {
        var channel = channelPool.Get();
        channel.OnDisrupted += Restore!;
        if (restored)
        {
            Event.WriteIfEnabled(RabbitDiagnostics.ChannelRestore, new {Channel = channel});
        }

        Ensure.Produce(channel.Channel, parameters);
        return channel;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    private void Restore(object sender, ChannelDisruptedEventArgs e)
    {
        CloseCurrentChannel();
        channelAccessor = CreateChannelAccessor(true);
    }

    private void CloseCurrentChannel()
    {
        if (!channelAccessor.IsValueCreated)
        {
            return;
        }

        var channel = channelAccessor.Value;
        channel.OnDisrupted -= Restore!;
        channel.Dispose();
    }

    public async Task Send(string routingKey, TMessage message, CancellationToken cancellationToken)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        await using var scope = serviceProvider.CreateAsyncScope();

        var basicProperties = channelAccessor.Value.Channel.CreateBasicProperties();
        var nativeProperties = new RabbitProducerProperties(basicProperties, parameters);
        var context = new ProducerContext<string, TMessage, RabbitProducerProperties>(
            scope.ServiceProvider, nativeProperties, routingKey, message);

        await pipeline.Invoke(context, cancellationToken).ConfigureAwait(false);
    }

    private Task SendMessage(
        ProducerContext<string, TMessage, RabbitProducerProperties> context,
        CancellationToken cancellationToken)
    {
        var channelAdapter = channelAccessor.Value;
        var channel = channelAdapter.Channel;

        var waitForConfirms = parameters.PublishingTimeout.HasValue;
        if (waitForConfirms)
        {
            channel.ConfirmSelect();
        }

        channel.BasicPublish(
            parameters.ExchangeName,
            context.Key,
            true,
            context.NativeProperties.BasicProperties,
            context.NativeProperties.Body);
        Event.WriteIfEnabled(CommonDiagnostics.MessagePublished, new { parameters.ExchangeName });

        if (waitForConfirms)
        {
            channel.WaitForConfirmsOrDie(parameters.PublishingTimeout!.Value);
        }

        return Task.CompletedTask;
    }

    public void Dispose() => CloseCurrentChannel();
}