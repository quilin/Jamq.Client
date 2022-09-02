namespace Jamq.Client.Abstractions.Producing;

/// <summary>
/// Producer builder
/// </summary>
public interface IProducerBuilder
{
    /// <summary>
    /// Add client-agnostic middleware to pipeline
    /// </summary>
    /// <param name="middleware">Middleware</param>
    /// <returns>Builder itself for chaining</returns>
    IProducerBuilder With(Func<ProducerDelegate, ProducerDelegate> middleware);

    /// <summary>
    /// Add message-agnostic middleware to pipeline
    /// </summary>
    /// <param name="middleware">Middleware</param>
    /// <typeparam name="TNativeProperties">Client-specific properties</typeparam>
    /// <returns>Builder itself for chaining</returns>
    IProducerBuilder With<TNativeProperties>(Func<ProducerDelegate<TNativeProperties>, ProducerDelegate<TNativeProperties>> middleware);

    /// <summary>
    /// Add specific middleware to pipeline
    /// </summary>
    /// <param name="middleware">Middleware</param>
    /// <typeparam name="TKey">Message key type</typeparam>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <typeparam name="TNativeProperties">Client-specific properties</typeparam>
    /// <returns>Builder itself for chaining</returns>
    IProducerBuilder With<TKey, TMessage, TNativeProperties>(Func<ProducerDelegate<TKey, TMessage, TNativeProperties>, ProducerDelegate<TKey, TMessage, TNativeProperties>> middleware);

    /// <summary>
    /// Add middleware of given type to the pipeline
    /// </summary>
    /// <param name="middlewareType">Middleware type</param>
    /// <param name="args">Middleware constructor arguments</param>
    /// <returns>Builder itself for chaining</returns>
    IProducerBuilder WithMiddleware(Type middlewareType, params object[] args);

    /// <summary>
    /// Remove all middlewares from pipeline
    /// </summary>
    /// <returns>Builder itself for chaining</returns>
    IProducerBuilder Flush();

    /// <summary>
    /// Extract builder service provider
    /// </summary>
    /// <returns></returns>
    internal IServiceProvider GetServiceProvider();

    /// <summary>
    /// Extract matching registered middlewares to create pipeline for certain producer
    /// </summary>
    /// <typeparam name="TKey">Message key</typeparam>
    /// <typeparam name="TMessage">Message</typeparam>
    /// <typeparam name="TProperties">Native properties</typeparam>
    /// <returns>Middleware sequence</returns>
    internal IEnumerable<Func<ProducerDelegate<TKey, TMessage, TProperties>, ProducerDelegate<TKey, TMessage, TProperties>>> GetMiddlewares<TKey, TMessage, TProperties>();
}