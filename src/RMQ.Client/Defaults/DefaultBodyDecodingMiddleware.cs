using System.Text.Json;
using RabbitMQ.Client.Events;
using RMQ.Client.Abstractions.Consuming;

namespace RMQ.Client.Defaults;

public class DefaultBodyDecodingMiddleware<TMessage> : IConsumerMiddleware<TMessage, BasicDeliverEventArgs>
{
    public Task<ProcessResult> InvokeAsync(
        ConsumerContext<TMessage, BasicDeliverEventArgs> context,
        ConsumerDelegate<TMessage, BasicDeliverEventArgs> next,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<TMessage>(
            context.NativeProperties.Body.Span, DefaultBodyEncodingSettings.SerializerOptions);
        context.Message = message;
        return next.Invoke(context, cancellationToken);
    }
}