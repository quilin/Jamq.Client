using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.Producing;

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