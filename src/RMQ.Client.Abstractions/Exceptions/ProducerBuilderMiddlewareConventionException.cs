using RMQ.Client.Abstractions.Producing;

namespace RMQ.Client.Abstractions.Exceptions;

public class ProducerBuilderMiddlewareConventionException : InvalidOperationException
{
    private ProducerBuilderMiddlewareConventionException(string message) : base(message)
    {
    }

    internal static ProducerBuilderMiddlewareConventionException NoInvokeAsyncMethod(Type type) =>
        new($"Middleware {type.FullName} has to have a method {nameof(IProducerMiddleware.InvokeAsync)}");

    internal static ProducerBuilderMiddlewareConventionException AmbiguousInvokeAsyncMethods(Type type) =>
        new($"Middleware {type.FullName} has multiple {nameof(IProducerMiddleware.InvokeAsync)} methods");

    internal static ProducerBuilderMiddlewareConventionException MismatchParameters(Type type) =>
        new($"Middleware {type.FullName} method {nameof(IProducerMiddleware.InvokeAsync)} has to have first parameter of type {nameof(ProducerContext)} and last of type {nameof(CancellationToken)}");

    internal static ProducerBuilderMiddlewareConventionException NotSupported() =>
        new("Not supported");
}