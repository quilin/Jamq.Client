using RMQ.Client.Abstractions.Consuming;

namespace RMQ.Client.Abstractions.Exceptions;

public class ConsumerBuilderMiddlewareConventionException : InvalidOperationException
{
    private ConsumerBuilderMiddlewareConventionException(string message) : base(message)
    {
    }

    internal static ConsumerBuilderMiddlewareConventionException NoInvokeAsyncMethod(Type type) =>
        new($"Middleware {type.FullName} has to have a method {nameof(IConsumerMiddleware.InvokeAsync)}");

    internal static ConsumerBuilderMiddlewareConventionException AmbiguousInvokeAsyncMethods(Type type) =>
        new($"Middleware {type.FullName} has multiple {nameof(IConsumerMiddleware.InvokeAsync)} methods");

    internal static ConsumerBuilderMiddlewareConventionException MismatchParameters(Type type) =>
        new($"Middleware {type.FullName} method {nameof(IConsumerMiddleware.InvokeAsync)} has to have first parameter of type {nameof(ConsumerContext)} and last of type {nameof(CancellationToken)}");

    internal static ConsumerBuilderMiddlewareConventionException NotSupported() => new("Not supported");
}