using RabbitMQ.Client;

namespace RMQ.Client.Abstractions.Producing;

public class RabbitProducerProperties
{
    internal RabbitProducerProperties(
        IBasicProperties basicProperties)
    {
        BasicProperties = basicProperties;
    }

    public IBasicProperties BasicProperties { get; }

    public byte[]? Body { get; set; }
}