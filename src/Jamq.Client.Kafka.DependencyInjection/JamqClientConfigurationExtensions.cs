using Confluent.Kafka;
using Jamq.Client.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Jamq.Client.Kafka.DependencyInjection;

public static class JamqClientConfigurationExtensions
{
    public static JamqClientConfiguration UseKafka(
        this JamqClientConfiguration configuration, ClientConfig parameters) =>
        UseKafka(configuration, _ => parameters);

    public static JamqClientConfiguration UseKafka(
        this JamqClientConfiguration configuration,
        Func<IServiceProvider, ClientConfig> parametersProvider)
    {
        configuration.GetServiceCollection().AddSingleton(parametersProvider);
        configuration.EnrichWithClientDefaults(b => b, b => b);
        return configuration;
    }
}