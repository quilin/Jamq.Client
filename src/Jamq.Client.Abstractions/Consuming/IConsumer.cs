namespace Jamq.Client.Abstractions.Consuming;

/// <summary>
/// Consumer
/// </summary>
public interface IConsumer : IDisposable
{
    /// <summary>
    /// Start listening to broker messages
    /// </summary>
    void Subscribe();

    /// <summary>
    /// Cancel the subscription
    /// </summary>
    void Cancel();

    /// <summary>
    /// Check whether the consumer is idle at the moment
    /// </summary>
    /// <returns></returns>
    bool IsIdle();
}