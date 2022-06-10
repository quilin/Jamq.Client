using System.Text.Json;
using RMQ.Client.Abstractions.Producing;

namespace RMQ.Client.Defaults;

internal class DefaultBodyEncodingMiddleware : IProducerMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task InvokeAsync(
        ProducerContext context,
        ProducerDelegate next)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(context.Message, SerializerOptions);
        context.Body = body;
        return next.Invoke(context);
    }
}