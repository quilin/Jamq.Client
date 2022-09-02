using Microsoft.Extensions.DependencyInjection;
using RMQ.Client.Abstractions.Consuming;
using RMQ.Client.Abstractions.Producing;

namespace RMQ.Client.DependencyInjection;

/// <summary>
/// Extensions for registration dependencies of RMQ.Client
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register required dependencies for RMQ Client
    /// <remarks>If you don't want to use default for either consumer or producer, you can use <see cref="IProducerBuilder.Flush" /> method of the builder in defaults factory parameters</remarks>
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">MQ implementation registration</param>
    /// <param name="producerBuilderDefaults">Default producer builder configuration</param>
    /// <param name="consumerBuilderDefaults">Default consumer builder configuration</param>
    /// <returns>Service collection itself for chaining</returns>
    public static IServiceCollection AddRmqClient(
        this IServiceCollection services,
        Action<RmqClientConfiguration> configure,
        Func<IProducerBuilder, IProducerBuilder>? producerBuilderDefaults = null,
        Func<IConsumerBuilder, IConsumerBuilder>? consumerBuilderDefaults = null)
    {
        var rmqClientBuilder = new RmqClientConfiguration(services);
        configure.Invoke(rmqClientBuilder);

        rmqClientBuilder.Register(producerBuilderDefaults, consumerBuilderDefaults);

        return services;
    }
}