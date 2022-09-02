using System.Text.Json;
using RabbitMQ.Client.Events;
using Jamq.Client.Abstractions.Consuming;

namespace Jamq.Client.Rabbit.Defaults;

public class DefaultRabbitBodyDecodingMiddleware<TMessage> : IConsumerMiddleware<string, TMessage, BasicDeliverEventArgs>
{
    public Task<ProcessResult> InvokeAsync(
        ConsumerContext<string, TMessage, BasicDeliverEventArgs> context,
        ConsumerDelegate<string, TMessage, BasicDeliverEventArgs> next,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<TMessage>(
            context.NativeProperties.Body.Span, DefaultBodyEncodingSettings.SerializerOptions);
        context.Message = message;

        return next.Invoke(context, cancellationToken);
    }
}