namespace RMQ.Client.Abstractions.Consuming;

public delegate Task<ProcessResult> ConsumerDelegate(
    ConsumerContext context,
    CancellationToken cancellationToken);

public delegate Task<ProcessResult> ConsumerDelegate<TMessage>(
    ConsumerContext<TMessage> context,
    CancellationToken cancellationToken);