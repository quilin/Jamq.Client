using System.Text.Json;
using RabbitMQ.Client.Events;
using RMQ.Client.Abstractions.Consuming;
using RMQ.Client.Abstractions.Producing;

namespace RMQ.Client.Defaults;

public class DefaultBodyEncodingMiddleware : IProducerMiddleware, IConsumerMiddleware<BasicDeliverEventArgs>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task InvokeAsync(
        ProducerContext context,
        ProducerDelegate next,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(context.Message, SerializerOptions);
        context.Body = body;
        return next.Invoke(context, cancellationToken);
    }

    public Task<ProcessResult> InvokeAsync<TMessage>(
        ConsumerContext<BasicDeliverEventArgs, TMessage> context,
        ConsumerDelegate<BasicDeliverEventArgs, TMessage> next,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<TMessage>(context.NativeProperties.Body.Span, SerializerOptions);
        context.Message = message;
        return next.Invoke(context, cancellationToken);
    }
}