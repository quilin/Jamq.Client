using Microsoft.Extensions.DependencyInjection;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Producing;

namespace Jamq.Client.DependencyInjection;

/// <summary>
/// Extensions for registration dependencies of Jamq.Client
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
    public static IServiceCollection AddJamqClient(
        this IServiceCollection services,
        Action<JamqClientConfiguration> configure,
        Func<IProducerBuilder, IProducerBuilder>? producerBuilderDefaults = null,
        Func<IConsumerBuilder, IConsumerBuilder>? consumerBuilderDefaults = null)
    {
        var jamqClientConfiguration = new JamqClientConfiguration(services);
        configure.Invoke(jamqClientConfiguration);

        jamqClientConfiguration.Register(producerBuilderDefaults, consumerBuilderDefaults);

        return services;
    }
}