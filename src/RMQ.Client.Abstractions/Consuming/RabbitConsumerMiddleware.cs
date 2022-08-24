using RabbitMQ.Client.Events;

namespace RMQ.Client.Abstractions.Consuming;

/// <summary>
/// Base Rabbit-specific consumer middleware
/// </summary>
public abstract class RabbitConsumerMiddleware : IConsumerMiddleware<BasicDeliverEventArgs>
{
    public abstract Task<ProcessResult> InvokeAsync<TMessage>(
        ConsumerContext<BasicDeliverEventArgs, TMessage> context,
        ConsumerDelegate<BasicDeliverEventArgs, TMessage> next,
        CancellationToken cancellationToken);
}