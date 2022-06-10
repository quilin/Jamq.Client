using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.Connection;
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
        Func<IProducerBuilder, IProducerBuilder>? producerBuilderDefaults = null)
    {
        services
            .AddSingleton(parameters)
            .AddSingleton(CreateConnectionFactory(parameters))
            .AddSingleton<IProducerChannelPool, ChannelPool>()
            .AddScoped<DefaultBodyEncodingMiddleware>();

        producerBuilderDefaults ??= builder => builder
            .WithMiddleware<DefaultBodyEncodingMiddleware>();
        services.AddTransient(provider => producerBuilderDefaults(new ProducerBuilder(provider)));

        return services;
    }

    private static IConnectionFactory CreateConnectionFactory(RabbitConnectionParameters parameters) =>
        new ConnectionFactory
        {
            Endpoint = new AmqpTcpEndpoint(new Uri(parameters.EndpointUrl)),
            ClientProperties = new Dictionary<string, object>
            {
                ["version"] = "0.0.1"
            }
        };
}