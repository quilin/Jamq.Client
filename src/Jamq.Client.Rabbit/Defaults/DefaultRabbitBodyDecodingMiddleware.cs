using System.Text.Json;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Consuming;

namespace Jamq.Client.Rabbit.Defaults;

public class DefaultRabbitBodyDecodingMiddleware<TMessage> : IConsumerMiddleware<string, TMessage, RabbitConsumerProperties>
{
    public Task<ProcessResult> InvokeAsync(
        ConsumerContext<string, TMessage, RabbitConsumerProperties> context,
        ConsumerDelegate<string, TMessage, RabbitConsumerProperties> next,
        CancellationToken cancellationToken)
    {
        var body = context.NativeProperties.BasicDeliverEventArgs.Body;
        var message = JsonSerializer.Deserialize<TMessage>(body.Span, DefaultBodyEncodingSettings.SerializerOptions);
        context.Message = message;

        return next.Invoke(context, cancellationToken);
    }
}