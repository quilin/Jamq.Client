using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Connection;

namespace Jamq.Client.Rabbit.Consuming;

public static class ConsumerBuilderExtensions
{
    /// <summary>
    /// Build consumer for RabbitMQ
    /// </summary>
    /// <param name="builder">Consumer builder</param>
    /// <param name="parameters">RabbitMQ consumer parameters</param>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <typeparam name="TProcessor">Message processor type</typeparam>
    /// <returns>Consumer</returns>
    public static IConsumer BuildRabbit<TMessage, TProcessor>(
        this IConsumerBuilder builder,
        RabbitConsumerParameters parameters)
        where TProcessor : IProcessor<string, TMessage>
    {
        var components = builder.GetMiddlewares<string, TMessage, RabbitConsumerProperties>();

        var serviceProvider = builder.GetServiceProvider();
        var channelPool = serviceProvider.GetRequiredService<IConsumerChannelPool>();
        var logger = serviceProvider.GetService<ILogger<RabbitConsumer<TMessage, TProcessor>>>();

        return new RabbitConsumer<TMessage, TProcessor>(channelPool, serviceProvider, parameters, logger, components);
    }
}