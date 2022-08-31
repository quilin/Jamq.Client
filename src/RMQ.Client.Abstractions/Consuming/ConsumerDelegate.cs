namespace RMQ.Client.Abstractions.Consuming;

public delegate Task<ProcessResult> ConsumerDelegate(
    ConsumerContext context,
    CancellationToken cancellationToken);

public delegate Task<ProcessResult> ConsumerDelegate<TNativeProperties>(
    ConsumerContext<TNativeProperties> context,
    CancellationToken cancellationToken);

public delegate Task<ProcessResult> ConsumerDelegate<TMessage, TNativeProperties>(
    ConsumerContext<TMessage, TNativeProperties> context,
    CancellationToken cancellationToken);