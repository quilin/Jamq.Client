using Confluent.Kafka;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Kafka.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Jamq.Client.Tests.Kafka;

public class KafkaFixture : IDisposable
{
    public IServiceCollection ServiceCollection { get; }
    public IServiceProvider ServiceProvider => providerFactory.CreateServiceProvider(ServiceCollection);

    private readonly DefaultServiceProviderFactory providerFactory;

    public KafkaFixture()
    {
        providerFactory = new DefaultServiceProviderFactory();
        ServiceCollection = providerFactory.CreateBuilder(new ServiceCollection());
        ServiceCollection.AddJamqClient(config => config
            .UseKafka(new ClientConfig
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = SecurityProtocol.Plaintext
            }));
        ServiceCollection.AddSingleton<KafkaProducerBuilderShould.TestProcessor>();
    }

    public void Dispose()
    {
    }
}