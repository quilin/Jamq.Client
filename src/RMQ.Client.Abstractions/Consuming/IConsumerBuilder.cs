namespace RMQ.Client.Abstractions.Consuming;

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
    /// <typeparam name="TNativeProperties">Client specific incoming message wrapper type</typeparam>
    /// <typeparam name="TMessage">Incoming message type</typeparam>
    /// <returns>Builder itself for chaining</returns>
    IConsumerBuilder With<TMessage, TNativeProperties>(
        Func<ConsumerDelegate<TMessage, TNativeProperties>, ConsumerDelegate<TMessage, TNativeProperties>> middleware);

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
    /// Build consumer for RabbitMQ
    /// </summary>
    /// <param name="parameters">RabbitMQ consumer parameters</param>
    /// <typeparam name="TProcessor">Message processor type</typeparam>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <returns>Consumer</returns>
    IConsumer BuildRabbit<TMessage, TProcessor>(RabbitConsumerParameters parameters)
        where TProcessor : IProcessor<TMessage>;
}