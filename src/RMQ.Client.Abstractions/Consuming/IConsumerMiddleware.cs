﻿namespace RMQ.Client.Abstractions.Consuming;

public interface IConsumerMiddleware
{
    Task<ProcessResult> InvokeAsync(
        ConsumerContext context,
        ConsumerDelegate next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Consumer pipeline middleware
/// </summary>
public interface IConsumerMiddleware<TNativeProperties>
{
    /// <summary>
    /// Consume handling method
    /// </summary>
    /// <param name="context">The <see cref="ConsumerContext{TNativeProperties, TMessage}"/> for the current consume</param>
    /// <param name="next">The delegate representing the remaining middleware in the consume pipelines</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware</returns>
    Task<ProcessResult> InvokeAsync(
        ConsumerContext<TNativeProperties> context,
        ConsumerDelegate<TNativeProperties> next,
        CancellationToken cancellationToken);
}

public interface IConsumerMiddleware<TMessage, TNativeProperties>
{
    Task<ProcessResult> InvokeAsync(
        ConsumerContext<TMessage, TNativeProperties> context,
        ConsumerDelegate<TMessage, TNativeProperties> next,
        CancellationToken cancellationToken);
}