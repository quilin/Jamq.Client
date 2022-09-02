namespace Jamq.Client.Abstractions.Producing;

/// <summary>
/// Client-agnostic producer pipeline middleware
/// </summary>
public interface IProducerMiddleware
{
    /// <summary>
    /// Produce handling method
    /// </summary>
    /// <param name="context">The <see cref="ProducerContext"/> for current produce</param>
    /// <param name="next">The delegate representing the remaining middleware in the produce pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware</returns>
    Task InvokeAsync(ProducerContext context, ProducerDelegate next, CancellationToken cancellationToken);
}

/// <summary>
/// Message-agnostic producer pipeline middleware
/// </summary>
/// <typeparam name="TNativeProperties">Client-specific properties</typeparam>
public interface IProducerMiddleware<TNativeProperties>
{
    /// <summary>
    /// Produce handling method
    /// </summary>
    /// <param name="context">The <see cref="ProducerContext"/> for current produce</param>
    /// <param name="next">The delegate representing the remaining middleware in the produce pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware</returns>
    Task InvokeAsync(
        ProducerContext<TNativeProperties> context,
        ProducerDelegate<TNativeProperties> next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Message-agnostic producer pipeline middleware
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TMessage"></typeparam>
/// <typeparam name="TNativeProperties">Client-specific properties</typeparam>
public interface IProducerMiddleware<TKey, TMessage, TNativeProperties>
{
    /// <summary>
    /// Produce handling method
    /// </summary>
    /// <param name="context">The <see cref="ProducerContext"/> for current produce</param>
    /// <param name="next">The delegate representing the remaining middleware in the produce pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware</returns>
    Task InvokeAsync(
        ProducerContext<TKey, TMessage, TNativeProperties> context,
        ProducerDelegate<TKey, TMessage, TNativeProperties> next,
        CancellationToken cancellationToken);
}