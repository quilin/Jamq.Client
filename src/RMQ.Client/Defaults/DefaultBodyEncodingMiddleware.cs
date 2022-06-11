using System.Text.Json;
using RMQ.Client.Abstractions.Consuming;
using RMQ.Client.Abstractions.Producing;

namespace RMQ.Client.Defaults;

public class DefaultBodyEncodingMiddleware : IProducerMiddleware, IConsumerMiddleware
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

    public Task<ProcessResult> InvokeAsync<TMessage>(
        ConsumerContext<TMessage> context,
        ConsumerDelegate<TMessage> next,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<TMessage>(context.NativeDeliverEvent.Body.Span, SerializerOptions);
        context.Message = message;
        return next.Invoke(context, cancellationToken);
    }
}