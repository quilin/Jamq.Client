using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.Connection;
using Jamq.Client.Rabbit.Defaults;

namespace Jamq.Client.Rabbit.DependencyInjection;

public static class JamqClientConfigurationExtensions
{
    public static JamqClientConfiguration UseRabbit(
        this JamqClientConfiguration configuration, RabbitConnectionParameters parameters) =>
        UseRabbit(configuration, _ => parameters);

    public static JamqClientConfiguration UseRabbit(
        this JamqClientConfiguration configuration,
        Func<IServiceProvider, RabbitConnectionParameters> parametersProvider)
    {
        configuration.GetServiceCollection()
            .AddSingleton(parametersProvider)
            .AddSingleton(sp => CreateConnectionFactory(parametersProvider.Invoke(sp)))
            .AddSingleton<IProducerChannelPool, ChannelPool>()
            .AddSingleton<IConsumerChannelPool, ChannelPool>()
            .AddTransient(typeof(DefaultDiagnosticMiddleware<>))
            .AddTransient(typeof(DefaultCodecMiddleware<>));

        configuration.EnrichWithClientDefaults(
            builder => builder
                .WithMiddleware(typeof(DefaultDiagnosticMiddleware<>))
                .WithMiddleware(typeof(DefaultCodecMiddleware<>)),
            builder => builder
                .WithMiddleware(typeof(DefaultDiagnosticMiddleware<>))
                .WithMiddleware(typeof(DefaultCodecMiddleware<>)));

        return configuration;
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
                ["product"] = "Jamq.Client",
                ["version"] = "0.5.3"
            }
        };
}