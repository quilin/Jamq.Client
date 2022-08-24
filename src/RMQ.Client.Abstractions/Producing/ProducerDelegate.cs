namespace RMQ.Client.Abstractions.Producing;

public delegate Task ProducerDelegate(ProducerContext context, CancellationToken cancellationToken);

public delegate Task ProducerDelegate<TNativeProperties>(
    ProducerContext<TNativeProperties> context,
    CancellationToken cancellationToken);