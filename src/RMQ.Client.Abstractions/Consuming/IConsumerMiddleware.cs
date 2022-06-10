namespace RMQ.Client.Abstractions.Consuming;

/// <summary>
/// Consumer pipeline middleware
/// </summary>
public interface IConsumerMiddleware
{
    /// <summary>
    /// Consume handling method
    /// </summary>
    /// <param name="context">The <see cref="ConsumerContext{TMessage}"/> for the current consume</param>
    /// <param name="next">The delegate representing the remaining middleware in the consume pipelines</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware</returns>
    Task<ProcessResult> InvokeAsync<TMessage>(
        ConsumerContext<TMessage> context,
        ConsumerDelegate<TMessage> next,
        CancellationToken cancellationToken);
}