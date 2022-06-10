namespace RMQ.Client.Abstractions.Consuming;

/// <summary>
/// Broker message processor
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public interface IProcessor<in TMessage>
{
    /// <summary>
    /// Handle the message
    /// </summary>
    /// <param name="message">Incoming message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    Task<ProcessResult> Process(TMessage message, CancellationToken cancellationToken);
}