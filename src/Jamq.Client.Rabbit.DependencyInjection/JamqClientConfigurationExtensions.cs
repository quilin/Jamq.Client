using Jamq.Client.Abstractions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.Connection;
using Jamq.Client.Rabbit.Connection.Adapters;
using Jamq.Client.Rabbit.Defaults;

namespace Jamq.Client.Rabbit.DependencyInjection;

public static class JamqClientConfigurationExtensions
{
    public static JamqClientConfiguration UseRabbit(
        this JamqClientConfiguration configuration) =>
        UseRabbit(configuration, _ => new RabbitConnectionParameters());

    public static JamqClientConfiguration UseRabbit(
        this JamqClientConfiguration configuration, RabbitConnectionParameters parameters) =>
        UseRabbit(configuration, _ => parameters);

    public static JamqClientConfiguration UseRabbit(
        this JamqClientConfiguration configuration,
        Func<IServiceProvider, RabbitConnectionParameters> parametersProvider)
    {
        configuration.GetServiceCollection()
            .AddSingleton(parametersProvider)
            .AddSingleton<IProducerChannelProvider, ChannelProvider>(ChannelPoolProvider)
            .AddSingleton<IConsumerChannelProvider, ChannelProvider>(ChannelPoolProvider)
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

    private static ChannelProvider ChannelPoolProvider(IServiceProvider serviceProvider)
    {
        var parameters = serviceProvider.GetService<RabbitConnectionParameters>() ??
            new RabbitConnectionParameters();
        var connectionFactory = serviceProvider.GetService<IAsyncConnectionFactory>() ??
            new ConnectionFactory
            {
                Endpoint = new AmqpTcpEndpoint(new Uri(parameters.EndpointUrl)),
                UserName = parameters.UserName,
                Password = parameters.Password,
                VirtualHost = parameters.VirtualHost,
            };

        connectionFactory.DispatchConsumersAsync = true;
        connectionFactory.ClientProperties = new Dictionary<string, object>
        {
            ["platform"] = ".NET",
            ["platform-version"] = Environment.Version.ToString(),
            ["product"] = Event.SourceName,
            ["version"] = Event.VersionName
        };
        return new ChannelProvider(connectionFactory);
    }
}