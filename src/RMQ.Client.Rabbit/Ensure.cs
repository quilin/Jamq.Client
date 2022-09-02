using RabbitMQ.Client;
using RMQ.Client.Rabbit.Consuming;
using RMQ.Client.Rabbit.Producing;

namespace RMQ.Client.Rabbit;

internal static class Ensure
{
    public static void Produce(IModel channel, RabbitProducerParameters parameters)
    {
        channel.ExchangeDeclare(
            parameters.ExchangeName,
            parameters.ExchangeType.ToString().ToLower(),
            true);
    }

    public static QueueDeclareOk Consume(IModel channel, RabbitConsumerParameters parameters)
    {
        if (parameters.ExchangeName is not null)
        {
            channel.ExchangeDeclare(parameters.ExchangeName, parameters.ExchangeType.ToString().ToLower(), true);
        }

        var queueArguments = new Dictionary<string, object>();
        var queueName = parameters.Exclusive
            ? parameters.QueueName.WithRandomSuffix()
            : parameters.QueueName;
        parameters.DeclaredQueueName = queueName;

        if (parameters.DeadLetterExchange is not null)
        {
            channel.ExchangeDeclare(parameters.DeadLetterExchange, RabbitMQ.Client.ExchangeType.Fanout, true);
            var dlqName = $"{parameters.DeadLetterExchange}-dlq";
            channel.QueueDeclare(dlqName, true, false, false);
            channel.QueueBind(dlqName, parameters.DeadLetterExchange, string.Empty);
            
            queueArguments.Add("x-dead-letter-exchange", parameters.DeadLetterExchange);
            queueArguments.Add("x-dead-letter-routing-key", queueName);
        }

        var result = channel.QueueDeclare(queueName, true, parameters.Exclusive, false, queueArguments);
        if (parameters is { RoutingKeys: not null, ExchangeName: not null })
        {
            foreach (var routingKey in parameters.RoutingKeys)
            {
                channel.QueueBind(queueName, parameters.ExchangeName, routingKey);
            }
        }

        if (parameters.ProcessingOrder != ProcessingOrder.Unmanaged)
        {
            channel.BasicQos(0, parameters.ProcessingOrder.Value, true);
        }

        return result;
    }
}