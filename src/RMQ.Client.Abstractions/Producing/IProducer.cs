namespace RMQ.Client.Abstractions.Producing;

/// <summary>
/// Producer
/// </summary>
public interface IProducer : IDisposable
{
    /// <summary>
    /// Send message to the broker
    /// </summary>
    /// <param name="routingKey">Routing key</param>
    /// <param name="message">Message</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <returns></returns>
    Task Send<TMessage>(string routingKey, TMessage message, CancellationToken cancellationToken);
}