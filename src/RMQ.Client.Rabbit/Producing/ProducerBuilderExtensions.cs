using Microsoft.Extensions.DependencyInjection;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.Rabbit.Connection;

namespace RMQ.Client.Rabbit.Producing;

public static class ProducerBuilderExtensions
{
    /// <summary>
    /// Build producer for RabbitMQ
    /// </summary>
    /// <param name="builder">Producer builder</param>
    /// <param name="parameters">RabbitMQ producer parameters</param>
    /// <returns>RabbitMq producer</returns>
    public static IProducer<string, TMessage> BuildRabbit<TMessage>(
        this IProducerBuilder builder,
        RabbitProducerParameters parameters)
    {
        var components = builder.GetMiddlewares<string, TMessage, RabbitProducerProperties>();
        var serviceProvider = builder.GetServiceProvider();
        var channelPool = serviceProvider.GetRequiredService<IProducerChannelPool>();
        return new RabbitProducer<TMessage>(channelPool, serviceProvider, parameters, components);
    }
}