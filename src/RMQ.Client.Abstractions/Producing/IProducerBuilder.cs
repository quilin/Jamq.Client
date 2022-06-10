namespace RMQ.Client.Abstractions.Producing;

/// <summary>
/// Producer builder
/// </summary>
public interface IProducerBuilder
{
    /// <summary>
    /// Add middleware to pipeline
    /// </summary>
    /// <param name="middleware">Middleware </param>
    /// <returns>Builder itself for chaining</returns>
    IProducerBuilder With(Func<ProducerDelegate, ProducerDelegate> middleware);

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
    IProducer BuildRabbit(RabbitProducerParameters parameters);

    /// <summary>
    /// Service provider
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}