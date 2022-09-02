namespace Jamq.Client.Abstractions.Consuming;

public delegate Task<ProcessResult> ConsumerDelegate(
    ConsumerContext context,
    CancellationToken cancellationToken);

public delegate Task<ProcessResult> ConsumerDelegate<TNativeProperties>(
    ConsumerContext<TNativeProperties> context,
    CancellationToken cancellationToken);

public delegate Task<ProcessResult> ConsumerDelegate<TKey, TMessage, TNativeProperties>(
    ConsumerContext<TKey, TMessage, TNativeProperties> context,
    CancellationToken cancellationToken);