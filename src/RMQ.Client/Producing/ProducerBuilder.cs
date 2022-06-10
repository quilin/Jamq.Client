using Microsoft.Extensions.DependencyInjection;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.Connection;

namespace RMQ.Client.Producing;

internal class ProducerBuilder : IProducerBuilder
{
    private readonly IList<Func<ProducerDelegate, ProducerDelegate>> components =
        new List<Func<ProducerDelegate, ProducerDelegate>>();

    public ProducerBuilder(
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }
    
    public IProducerBuilder With(Func<ProducerDelegate, ProducerDelegate> middleware)
    {
        components.Add(middleware);
        return this;
    }

    public IProducerBuilder Flush()
    {
        components.Clear();
        return this;
    }

    public IProducer BuildRabbit(RabbitProducerParameters parameters)
    {
        var channelPool = ServiceProvider.GetRequiredService<IProducerChannelPool>();
        return new Producer(channelPool, ServiceProvider, parameters, components);
    }

    public IServiceProvider ServiceProvider { get; }
}