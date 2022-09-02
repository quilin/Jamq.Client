namespace RMQ.Client.Abstractions.Producing;

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
    /// Build producer for RabbitMQ
    /// </summary>
    /// <param name="parameters">RabbitMQ producer parameters</param>
    /// <returns></returns>
    IProducer<string, TMessage> BuildRabbit<TMessage>(RabbitProducerParameters parameters);
}