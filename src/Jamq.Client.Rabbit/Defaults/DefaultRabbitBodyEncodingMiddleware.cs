using System.Text.Json;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;

namespace Jamq.Client.Rabbit.Defaults;

public class DefaultRabbitBodyEncodingMiddleware<TMessage> : IProducerMiddleware<string, TMessage, RabbitProducerProperties>
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