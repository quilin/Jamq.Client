﻿using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RMQ.Client.DependencyInjection;
using RMQ.Client.Rabbit.Connection;
using RMQ.Client.Rabbit.Defaults;

namespace RMQ.Client.Rabbit.DependencyInjection;

public static class RmqClientConfigurationExtensions
{
    public static RmqClientConfiguration UseRabbit(
        this RmqClientConfiguration configuration, RabbitConnectionParameters parameters) =>
        UseRabbit(configuration, _ => parameters);

    public static RmqClientConfiguration UseRabbit(
        this RmqClientConfiguration builder,
        Func<IServiceProvider, RabbitConnectionParameters> parametersProvider)
    {
        builder.GetServiceCollection()
            .AddSingleton(parametersProvider)
            .AddSingleton(sp => CreateConnectionFactory(parametersProvider.Invoke(sp)))
            .AddSingleton<IProducerChannelPool, ChannelPool>()
            .AddSingleton<IConsumerChannelPool, ChannelPool>()
            .AddTransient(typeof(DefaultRabbitBodyEncodingMiddleware<>))
            .AddTransient(typeof(DefaultRabbitBodyDecodingMiddleware<>));

        builder.EnrichWithClientDefaults(
            b => b.WithMiddleware(typeof(DefaultRabbitBodyEncodingMiddleware<>)),
            b => b.WithMiddleware(typeof(DefaultRabbitBodyDecodingMiddleware<>)));

        return builder;
    }

    private static IConnectionFactory CreateConnectionFactory(RabbitConnectionParameters parameters) =>
        new ConnectionFactory
        {
            Endpoint = new AmqpTcpEndpoint(new Uri(parameters.EndpointUrl)),
            DispatchConsumersAsync = true,
            ClientProperties = new Dictionary<string, object>
            {
                ["platform"] = ".NET",
                ["platform-version"] = Environment.Version.ToString(),
                ["product"] = "RMQ.Client",
                ["version"] = "0.3.1"
            }
        };
}