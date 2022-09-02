namespace RMQ.Client.Abstractions.Producing;

public delegate Task ProducerDelegate(ProducerContext context, CancellationToken cancellationToken);

public delegate Task ProducerDelegate<TNativeProperties>(
    ProducerContext<TNativeProperties> context,
    CancellationToken cancellationToken);

public delegate Task ProducerDelegate<TKey, TMessage, TNativeProperties>(
    ProducerContext<TKey, TMessage, TNativeProperties> context,
    CancellationToken cancellationToken);