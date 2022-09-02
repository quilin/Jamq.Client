using System.Text.Json;
using RMQ.Client.Abstractions.Producing;

namespace RMQ.Client.Defaults;

public class DefaultBodyEncodingMiddleware<TMessage> : IProducerMiddleware<string, TMessage, RabbitProducerProperties>
{
    public Task InvokeAsync(
        ProducerContext<string, TMessage, RabbitProducerProperties> context,
        ProducerDelegate<string, TMessage, RabbitProducerProperties> next,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(context.Message, DefaultBodyEncodingSettings.SerializerOptions);
        context.NativeProperties.Body = body;
        return next.Invoke(context, cancellationToken);
    }
}