namespace Jamq.Client.Abstractions.Consuming;

/// <summary>
/// Broker message processor
/// </summary>
/// <typeparam name="TKey">Message key type</typeparam>
/// <typeparam name="TMessage">Message type</typeparam>
public interface IProcessor<in TKey, in TMessage>
{
    /// <summary>
    /// Handle the message
    /// </summary>
    /// <param name="key">Message key</param>
    /// <param name="message">Incoming message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    Task<ProcessResult> Process(TKey key, TMessage message, CancellationToken cancellationToken);
}