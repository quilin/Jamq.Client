namespace Jamq.Client.Abstractions.Producing;

/// <summary>
/// Producer
/// </summary>
/// <typeparam name="TKey">Message key type</typeparam>
/// <typeparam name="TMessage">Message type</typeparam>
public interface IProducer<in TKey, in TMessage> : IDisposable
{
    /// <summary>
    /// Send message to the broker
    /// </summary>
    /// <param name="key">Routing key</param>
    /// <param name="message">Message</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task Send(TKey key, TMessage message, CancellationToken cancellationToken);
}