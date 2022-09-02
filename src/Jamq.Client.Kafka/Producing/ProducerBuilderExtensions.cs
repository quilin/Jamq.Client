using Confluent.Kafka;
using Jamq.Client.Abstractions.Producing;
using Microsoft.Extensions.DependencyInjection;

namespace Jamq.Client.Kafka.Producing;

public static class ProducerBuilderExtensions
{
    /// <summary>
    /// Build producer for Confluent.Kafka
    /// </summary>
    /// <param name="builder">Producer builder</param>
    /// <param name="parametersProvider">Producer configuration provider</param>
    /// <returns>RabbitMq producer</returns>
    public static Abstractions.Producing.IProducer<TKey, TMessage> BuildKafka<TKey, TMessage>(
        this IProducerBuilder builder,
        Func<IServiceProvider, ClientConfig, KafkaProducerParameters> parametersProvider)
    {
        var components = builder.GetMiddlewares<TKey, TMessage, Message<TKey, TMessage>>();
        var serviceProvider = builder.GetServiceProvider();
        var clientConfig = serviceProvider.GetRequiredService<ClientConfig>();
        var parameters = parametersProvider.Invoke(serviceProvider, clientConfig);
        return new KafkaProducer<TKey, TMessage>(serviceProvider, parameters, components);
    }

    /// <summary>
    /// Build producer for Confluent.Kafka
    /// </summary>
    /// <param name="builder">Producer builder</param>
    /// <param name="parametersProvider">Producer configuration provider</param>
    /// <returns>RabbitMq producer</returns>
    public static Abstractions.Producing.IProducer<TKey, TMessage> BuildKafka<TKey, TMessage>(
        this IProducerBuilder builder,
        Func<IServiceProvider, KafkaProducerParameters> parametersProvider) =>
        BuildKafka<TKey, TMessage>(builder, (sp, _) => parametersProvider.Invoke(sp));

    /// <summary>
    /// Build producer for Confluent.Kafka
    /// </summary>
    /// <param name="builder">Producer builder</param>
    /// <param name="parameters">Producer configuration</param>
    /// <returns>RabbitMq producer</returns>
    public static Abstractions.Producing.IProducer<TKey, TMessage> BuildKafka<TKey, TMessage>(
        this IProducerBuilder builder,
        KafkaProducerParameters parameters) =>
        BuildKafka<TKey, TMessage>(builder, _ => parameters);
}