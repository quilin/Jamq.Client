using System.Text.Json;
using RabbitMQ.Client.Events;
using RMQ.Client.Abstractions.Consuming;

namespace RMQ.Client.Defaults;

public class DefaultBodyDecodingMiddleware<TMessage> : IConsumerMiddleware<BasicDeliverEventArgs, TMessage>
{
    public Task<ProcessResult> InvokeAsync(
        ConsumerContext<BasicDeliverEventArgs, TMessage> context,
        ConsumerDelegate<BasicDeliverEventArgs, TMessage> next,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<TMessage>(
            context.NativeProperties.Body.Span, DefaultBodyEncodingSettings.SerializerOptions);
        context.Message = message;
        return next.Invoke(context, cancellationToken);
    }
}