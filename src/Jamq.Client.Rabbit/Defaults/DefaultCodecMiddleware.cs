using System.Text.Json;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Consuming;
using Jamq.Client.Rabbit.Producing;

namespace Jamq.Client.Rabbit.Defaults;

public abstract class DefaultCodecMiddleware
{
    protected static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}

public class DefaultCodecMiddleware<TMessage> : DefaultCodecMiddleware,
    IProducerMiddleware<string, TMessage, RabbitProducerProperties>,
    IConsumerMiddleware<string, TMessage, RabbitConsumerProperties>
{
    public Task InvokeAsync(
        ProducerContext<string, TMessage, RabbitProducerProperties> context,
        ProducerDelegate<string, TMessage, RabbitProducerProperties> next,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(context.Message, SerializerOptions);
        context.NativeProperties.Body = body;
        return next.Invoke(context, cancellationToken);
    }

    public Task<ProcessResult> InvokeAsync(
        ConsumerContext<string, TMessage, RabbitConsumerProperties> context,
        ConsumerDelegate<string, TMessage, RabbitConsumerProperties> next,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<TMessage>(
            context.NativeProperties.BasicDeliverEventArgs.Body.Span, SerializerOptions);
        context.Message = message;

        return next.Invoke(context, cancellationToken);
    }
}