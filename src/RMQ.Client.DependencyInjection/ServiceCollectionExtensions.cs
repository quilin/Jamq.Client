using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Consuming;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.Connection;
using RMQ.Client.Consuming;
using RMQ.Client.Defaults;
using RMQ.Client.Producing;

namespace RMQ.Client.DependencyInjection;

/// <summary>
/// Extensions for registration dependencies of RMQ.Client
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRmqClient(
        this IServiceCollection services,
        RabbitConnectionParameters parameters,
        Func<IProducerBuilder, IProducerBuilder>? producerBuilderDefaults = null,
        Func<IConsumerBuilder, IConsumerBuilder>? consumerBuilderDefaults = null)
    {
        services
            .AddSingleton(parameters)
            .AddSingleton(CreateConnectionFactory(parameters))
            .AddSingleton<IProducerChannelPool, ChannelPool>()
            .AddSingleton<IConsumerChannelPool, ChannelPool>()
            .AddScoped<DefaultBodyEncodingMiddleware>();

        producerBuilderDefaults ??= builder => builder
            .WithMiddleware<DefaultBodyEncodingMiddleware>();
        services.AddTransient(provider => producerBuilderDefaults(new ProducerBuilder(provider)));

        consumerBuilderDefaults ??= builder => builder
            .WithMiddleware<DefaultBodyEncodingMiddleware>();
        services.AddTransient(provider => consumerBuilderDefaults(new ConsumerBuilder(provider)));

        return services;
    }

    private static IConnectionFactory CreateConnectionFactory(RabbitConnectionParameters parameters) =>
        new ConnectionFactory
        {
            Endpoint = new AmqpTcpEndpoint(new Uri(parameters.EndpointUrl)),
            DispatchConsumersAsync = true,
            ClientProperties = new Dictionary<string, object>
            {
                ["version"] = "0.0.1"
            }
        };
}