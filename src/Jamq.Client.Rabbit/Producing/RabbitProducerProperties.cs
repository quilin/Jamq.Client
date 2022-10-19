using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.Producing;

public class RabbitProducerProperties
{
    internal RabbitProducerProperties(
        IBasicProperties basicProperties,
        RabbitProducerParameters producerParameters)
    {
        BasicProperties = basicProperties;
        ProducerParameters = producerParameters;
    }

    public IBasicProperties BasicProperties { get; }

    public RabbitProducerParameters ProducerParameters { get; }

    public byte[]? Body { get; set; }
}