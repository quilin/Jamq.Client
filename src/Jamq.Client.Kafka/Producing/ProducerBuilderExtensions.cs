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
    /// <param name="keySerializer">Key serializer</param>
    /// <param name="messageSerializer">Message serializer</param>
    /// <returns>RabbitMq producer</returns>
    public static Abstractions.Producing.IProducer<TKey, TMessage> BuildKafka<TKey, TMessage>(
        this IProducerBuilder builder,
        Func<IServiceProvider, ClientConfig, KafkaProducerParameters> parametersProvider,
        ISerializer<TKey>? keySerializer = null,
        ISerializer<TMessage>? messageSerializer = null)
    {
        var components = builder.GetMiddlewares<TKey, TMessage, KafkaProducerProperties<TKey, TMessage>>();
        var serviceProvider = builder.GetServiceProvider();
        var clientConfig = serviceProvider.GetRequiredService<ClientConfig>();
        var parameters = parametersProvider.Invoke(serviceProvider, clientConfig);
        return new KafkaProducer<TKey, TMessage>(
            serviceProvider, parameters, components, keySerializer, messageSerializer);
    }

    /// <summary>
    /// Build producer for Confluent.Kafka
    /// </summary>
    /// <param name="builder">Producer builder</param>
    /// <param name="parametersProvider">Producer configuration provider</param>
    /// <param name="keySerializer">Key serializer</param>
    /// <param name="messageSerializer">Message serializer</param>
    /// <returns>RabbitMq producer</returns>
    public static Abstractions.Producing.IProducer<TKey, TMessage> BuildKafka<TKey, TMessage>(
        this IProducerBuilder builder,
        Func<IServiceProvider, KafkaProducerParameters> parametersProvider,
        ISerializer<TKey>? keySerializer = null,
        ISerializer<TMessage>? messageSerializer = null) =>
        BuildKafka(builder, (sp, _) => parametersProvider.Invoke(sp), keySerializer, messageSerializer);

    /// <summary>
    /// Build producer for Confluent.Kafka
    /// </summary>
    /// <param name="builder">Producer builder</param>
    /// <param name="parameters">Producer configuration</param>
    /// <param name="keySerializer">Key serializer</param>
    /// <param name="messageSerializer">Message serializer</param>
    /// <returns>RabbitMq producer</returns>
    public static Abstractions.Producing.IProducer<TKey, TMessage> BuildKafka<TKey, TMessage>(
        this IProducerBuilder builder,
        KafkaProducerParameters parameters,
        ISerializer<TKey>? keySerializer = null,
        ISerializer<TMessage>? messageSerializer = null) =>
        BuildKafka(builder, _ => parameters, keySerializer, messageSerializer);
}