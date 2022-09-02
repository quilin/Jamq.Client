using Confluent.Kafka;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.Extensions.DependencyInjection;

namespace Jamq.Client.Kafka.Consuming;

public static class ConsumerBuilderExtensions
{
    /// <summary>
    /// Build consumer for Confluent.Kafka
    /// </summary>
    /// <param name="builder">Consumer builder</param>
    /// <param name="parametersProvider">Consumer configuration provider</param>
    /// <returns>Confluent.Kafka producer</returns>
    public static IConsumer BuildKafka<TKey, TMessage, TProcessor>(
        this IConsumerBuilder builder,
        Func<IServiceProvider, ClientConfig, KafkaConsumerParameters> parametersProvider)
        where TProcessor : IProcessor<TKey, TMessage>
    {
        var middlewares = builder.GetMiddlewares<TKey, TMessage, ConsumeResult<TKey, TMessage>>();
        var serviceProvider = builder.GetServiceProvider();
        var clientConfig = serviceProvider.GetRequiredService<ClientConfig>();
        var parameters = parametersProvider.Invoke(serviceProvider, clientConfig);
        return new KafkaConsumer<TKey, TMessage, TProcessor>(serviceProvider, parameters, middlewares);
    }

    /// <summary>
    /// Build consumer for Confluent.Kafka
    /// </summary>
    /// <param name="builder">Consumer builder</param>
    /// <param name="parametersProvider">Consumer configuration provider</param>
    /// <returns>Confluent.Kafka producer</returns>
    public static IConsumer BuildKafka<TKey, TMessage, TProcessor>(
        this IConsumerBuilder builder,
        Func<IServiceProvider, KafkaConsumerParameters> parametersProvider)
        where TProcessor : IProcessor<TKey, TMessage> =>
        BuildKafka<TKey, TMessage, TProcessor>(builder, (sp, _) => parametersProvider(sp));
    
    /// <summary>
    /// Build consumer for Confluent.Kafka
    /// </summary>
    /// <param name="builder">Consumer builder</param>
    /// <param name="parameters">Consumer configuration</param>
    /// <returns>Confluent.Kafka producer</returns>
    public static IConsumer BuildKafka<TKey, TMessage, TProcessor>(
        this IConsumerBuilder builder,
        KafkaConsumerParameters parameters)
        where TProcessor : IProcessor<TKey, TMessage> =>
        BuildKafka<TKey, TMessage, TProcessor>(builder, _ => parameters);
}