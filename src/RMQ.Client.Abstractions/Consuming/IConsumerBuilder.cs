namespace RMQ.Client.Abstractions.Consuming;

/// <summary>
/// Consumer builder
/// </summary>
public interface IConsumerBuilder
{
    /// <summary>
    /// Add middleware to the pipeline
    /// </summary>
    /// <param name="middleware"></param>
    /// <returns>Builder itself for changing</returns>
    IConsumerBuilder With(Func<ConsumerDelegate, ConsumerDelegate> middleware);

    /// <summary>
    /// Add middleware to the pipeline
    /// </summary>
    /// <param name="middlewareType"></param>
    /// <param name="args"></param>
    /// <returns></returns>
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
    /// <returns></returns>
    IConsumer BuildRabbit<TProcessor, TMessage>(RabbitConsumerParameters parameters)
        where TProcessor : IProcessor<TMessage>;

    /// <summary>
    /// Service provider
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}