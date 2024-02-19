using Jamq.Client.Rabbit.Consuming;
using Jamq.Client.Rabbit.Producing;
using RabbitMQ.Client;
using System.Collections.Immutable;

namespace Jamq.Client.Rabbit;

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
            channel.ExchangeDeclare(
                exchange: parameters.ExchangeName,
                type: parameters.ExchangeType.ToString().ToLower(),
                durable: true,
                arguments: parameters.AdditionalExchangeArguments ?? ImmutableDictionary<string, object>.Empty);
        }

        var queueArguments = new Dictionary<string, object>(
            parameters.AdditionalQueueArguments ?? ImmutableDictionary<string, object>.Empty);

        var queueName = parameters.Exclusive
            ? parameters.QueueName.WithRandomSuffix()
            : parameters.QueueName;
        parameters.DeclaredQueueName = queueName;

        if (parameters.DeadLetterExchange is not null)
        {
            queueArguments.Add("x-dead-letter-exchange", parameters.DeadLetterExchange);
            queueArguments.Add("x-dead-letter-routing-key", queueName);
        }

        var result = channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: parameters.Exclusive,
            autoDelete: false,
            arguments: queueArguments);

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