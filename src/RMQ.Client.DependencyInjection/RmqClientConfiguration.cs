using Microsoft.Extensions.DependencyInjection;
using RMQ.Client.Abstractions.Consuming;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.Consuming;
using RMQ.Client.Producing;

namespace RMQ.Client.DependencyInjection;

public class RmqClientConfiguration
{
    private readonly IServiceCollection serviceCollection;
    private readonly List<Func<IProducerBuilder, IProducerBuilder>> producerBuilderRegistrations = new();
    private readonly List<Func<IConsumerBuilder, IConsumerBuilder>> consumerBuilderRegistrations = new();

    public RmqClientConfiguration(IServiceCollection serviceCollection)
    {
        this.serviceCollection = serviceCollection;
    }

    internal IServiceCollection GetServiceCollection() => serviceCollection;

    internal void EnrichWithClientDefaults(
        Func<IProducerBuilder, IProducerBuilder> producerDefaults,
        Func<IConsumerBuilder, IConsumerBuilder> consumerDefaults)
    {
        producerBuilderRegistrations.Add(producerDefaults);
        consumerBuilderRegistrations.Add(consumerDefaults);
    }

    internal void Register(Func<IProducerBuilder, IProducerBuilder>? producerDefaults,
        Func<IConsumerBuilder, IConsumerBuilder>? consumerDefaults)
    {
        if (producerDefaults is not null) producerBuilderRegistrations.Add(producerDefaults);
        if (consumerDefaults is not null) consumerBuilderRegistrations.Add(consumerDefaults);

        serviceCollection
            .AddTransient(provider => producerBuilderRegistrations.Aggregate(
                (IProducerBuilder)new ProducerBuilder(provider),
                (seed, current) => current.Invoke(seed)))
            .AddTransient(provider => consumerBuilderRegistrations.Aggregate(
                (IConsumerBuilder)new ConsumerBuilder(provider),
                (seed, current) => current.Invoke(seed)));
    }
}