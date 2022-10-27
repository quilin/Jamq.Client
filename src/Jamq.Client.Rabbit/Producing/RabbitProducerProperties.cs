using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.Producing;

public class RabbitProducerProperties
{
    internal RabbitProducerProperties(
        IBasicProperties basicProperties,
        RabbitProducerParameters parameters)
    {
        BasicProperties = basicProperties;
        Parameters = parameters;
    }

    public IBasicProperties BasicProperties { get; }

    public RabbitProducerParameters Parameters { get; }

    public byte[]? Body { get; set; }
}