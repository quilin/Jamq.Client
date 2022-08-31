using System.Text.Json;
using RabbitMQ.Client.Events;
using RMQ.Client.Abstractions.Consuming;

namespace RMQ.Client.Defaults;

public class DefaultBodyDecodingMiddleware<TMessage> : IConsumerMiddleware<string, TMessage, BasicDeliverEventArgs>
{
    public Task<ProcessResult> InvokeAsync(
        ConsumerContext<string, TMessage, BasicDeliverEventArgs> context,
        ConsumerDelegate<string, TMessage, BasicDeliverEventArgs> next,
        CancellationToken cancellationToken)
    {
        context.Key = context.NativeProperties.RoutingKey;

        var message = JsonSerializer.Deserialize<TMessage>(
            context.NativeProperties.Body.Span, DefaultBodyEncodingSettings.SerializerOptions);
        context.Message = message;

        return next.Invoke(context, cancellationToken);
    }
}