using System.Text.Json;
using RMQ.Client.Abstractions.Producing;

namespace RMQ.Client.Defaults;

public class DefaultBodyEncodingMiddleware : IProducerMiddleware
{
    public Task InvokeAsync(
        ProducerContext context,
        ProducerDelegate next,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(context.Message, DefaultBodyEncodingSettings.SerializerOptions);
        context.Body = body;
        return next.Invoke(context, cancellationToken);
    }
}