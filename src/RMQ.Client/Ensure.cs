using RabbitMQ.Client;
using RMQ.Client.Abstractions;

namespace RMQ.Client;

internal static class Ensure
{
    public static void Produce(IModel channel, RabbitProducerParameters parameters)
    {
        channel.ExchangeDeclare(
            parameters.ExchangeName,
            parameters.ExchangeType.ToString().ToLower(),
            true);
    }
}