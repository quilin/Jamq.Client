namespace Jamq.Client.Abstractions.Consuming;

/// <summary>
/// Consumer pipeline middleware
/// </summary>
public interface IConsumerMiddleware
{
    /// <summary>
    /// Consume handling method
    /// </summary>
    /// <param name="context">The <see cref="ConsumerContext"/> for the current consume</param>
    /// <param name="next">The delegate representing the remaining middleware in the consume pipelines</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A <see cref="Task{ProcessResult}"/> that represents the execution of this middleware</returns>
    Task<ProcessResult> InvokeAsync(
        ConsumerContext context,
        ConsumerDelegate next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Consumer pipeline middleware
/// </summary>
/// <typeparam name="TNativeProperties">Client-based properties type</typeparam>
public interface IConsumerMiddleware<TNativeProperties>
{
    /// <summary>
    /// Consume handling method
    /// </summary>
    /// <param name="context">The <see cref="ConsumerContext{TNativeProperties}"/> for the current consume</param>
    /// <param name="next">The delegate representing the remaining middleware in the consume pipelines</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TNativeProperties">Client-based properties</typeparam>
    /// <returns>A <see cref="Task{ProcessResult}"/> that represents the execution of this middleware</returns>
    Task<ProcessResult> InvokeAsync(
        ConsumerContext<TNativeProperties> context,
        ConsumerDelegate<TNativeProperties> next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Consumer pipeline middleware
/// </summary>
/// <typeparam name="TKey">Message key type</typeparam>
/// <typeparam name="TMessage">Message type</typeparam>
/// <typeparam name="TNativeProperties">Client-based properties type</typeparam>
public interface IConsumerMiddleware<TKey, TMessage, TNativeProperties>
{
    /// <summary>
    /// Consume handling method
    /// </summary>
    /// <param name="context">The <see cref="ConsumerContext{TKey, TMessage, TNativeProperties}"/> for the current consume</param>
    /// <param name="next">The delegate representing the remaining middleware in the consume pipelines</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TNativeProperties">Client-based properties</typeparam>
    /// <returns>A <see cref="Task{ProcessResult}"/> that represents the execution of this middleware</returns>
    Task<ProcessResult> InvokeAsync(
        ConsumerContext<TKey, TMessage, TNativeProperties> context,
        ConsumerDelegate<TKey, TMessage, TNativeProperties> next,
        CancellationToken cancellationToken);
}