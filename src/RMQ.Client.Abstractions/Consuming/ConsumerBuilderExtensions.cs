namespace RMQ.Client.Abstractions.Consuming;

/// <summary>
/// Extensions for typed middleware configuration
/// </summary>
public static class ConsumerBuilderExtensions
{
    /// <summary>
    /// Add middleware of certain type to the pipeline
    /// </summary>
    /// <param name="builder">Consumer builder</param>
    /// <param name="args">Middleware constructor parameters that cannot be resolved by <see cref="IServiceProvider" /></param>
    /// <typeparam name="TMiddleware">Consumer middleware</typeparam>
    /// <returns>Builder itself for chaining</returns>
    public static IConsumerBuilder WithMiddleware<TMiddleware>(
        this IConsumerBuilder builder, params object[] args) =>
        builder.WithMiddleware(typeof(TMiddleware));
}