namespace Jamq.Client.Abstractions.Consuming;

/// <summary>
/// Consumer builder
/// </summary>
public interface IConsumerBuilder
{
    /// <summary>
    /// Add client-agnostic middleware to the pipeline
    /// </summary>
    /// <param name="middleware">Middleware</param>
    /// <returns>Builder itself for chaining</returns>
    IConsumerBuilder With(Func<ConsumerDelegate, ConsumerDelegate> middleware);

    /// <summary>
    /// Add client-specific middleware to the pipeline
    /// </summary>
    /// <param name="middleware">Middleware</param>
    /// <typeparam name="TNativeProperties">Client specific incoming message wrapper type</typeparam>
    /// <returns>Builder itself for chaining</returns>
    IConsumerBuilder With<TNativeProperties>(
        Func<ConsumerDelegate<TNativeProperties>, ConsumerDelegate<TNativeProperties>> middleware);

    /// <summary>
    /// Add client-and-message-specific middleware to the pipeline
    /// </summary>
    /// <param name="middleware">Middleware</param>
    /// <typeparam name="TKey">Message key type</typeparam>
    /// <typeparam name="TMessage">Incoming message type</typeparam>
    /// <typeparam name="TNativeProperties">Client specific incoming message wrapper type</typeparam>
    /// <returns>Builder itself for chaining</returns>
    IConsumerBuilder With<TKey, TMessage, TNativeProperties>(
        Func<ConsumerDelegate<TKey, TMessage, TNativeProperties>, ConsumerDelegate<TKey, TMessage, TNativeProperties>> middleware);

    /// <summary>
    /// Add middleware to the pipeline
    /// </summary>
    /// <param name="middlewareType"></param>
    /// <param name="args"></param>
    /// <returns>Builder itself for chaining</returns>
    IConsumerBuilder WithMiddleware(Type middlewareType, params object[] args);

    /// <summary>
    /// Remove all middlewares from the builder
    /// </summary>
    /// <returns>Builder itself for chaining</returns>
    IConsumerBuilder Flush();

    /// <summary>
    /// Extract builder service provider
    /// </summary>
    /// <returns></returns>
    internal IServiceProvider GetServiceProvider();

    /// <summary>
    /// Extract matching registered middlewares to create pipeline for certain consumer
    /// </summary>
    /// <typeparam name="TKey">Message key</typeparam>
    /// <typeparam name="TMessage">Message</typeparam>
    /// <typeparam name="TProperties">Native properties</typeparam>
    /// <returns>Middleware sequence</returns>
    internal IEnumerable<Func<ConsumerDelegate<TKey, TMessage, TProperties>, ConsumerDelegate<TKey, TMessage, TProperties>>> GetMiddlewares<TKey, TMessage, TProperties>();
}