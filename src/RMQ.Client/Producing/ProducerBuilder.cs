using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Exceptions;
using RMQ.Client.Abstractions.Producing;
using RMQ.Client.Connection;

namespace RMQ.Client.Producing;

internal class ProducerBuilder : IProducerBuilder
{
    private readonly IList<object> middlewares = new List<object>();
    private readonly IServiceProvider serviceProvider;

    public ProducerBuilder(
        IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public IProducerBuilder With(Func<ProducerDelegate, ProducerDelegate> middleware)
    {
        middlewares.Add(middleware);
        return this;
    }

    public IProducerBuilder With<TNativeProperties>(
        Func<ProducerDelegate<TNativeProperties>, ProducerDelegate<TNativeProperties>> middleware)
    {
        middlewares.Add(middleware);
        return this;
    }

    public IProducerBuilder WithMiddleware(Type middlewareType, params object[] args)
    {
        middlewares.Add((middlewareType, args));
        return this;
    }

    public IProducerBuilder Flush()
    {
        middlewares.Clear();
        return this;
    }

    public IProducer BuildRabbit(RabbitProducerParameters parameters)
    {
        var components = middlewares.Select(ToPipelineStep<IBasicProperties>);
        var channelPool = serviceProvider.GetRequiredService<IProducerChannelPool>();
        return new Producer(channelPool, serviceProvider, parameters, components);
    }

    private Func<ProducerDelegate<TNativeProperties>, ProducerDelegate<TNativeProperties>>
        ToPipelineStep<TNativeProperties>(object description) => description switch
    {
        // Lambda client-specific middleware
        Func<ProducerDelegate<TNativeProperties>, ProducerDelegate<TNativeProperties>> middleware => middleware,

        // Client-specific middleware
        (Type type, object[]) when typeof(IProducerMiddleware<TNativeProperties>).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
        {
            var middleware = (IProducerMiddleware<TNativeProperties>)context.ServiceProvider.GetRequiredService(type);
            return middleware.InvokeAsync(context, next, ct);
        },

        // Lambda client-agnostic middleware
        Func<ProducerDelegate, ProducerDelegate> clientAgnosticMiddleware => next => (context, ct) =>
        {
            var clientAgnosticDelegate = (ProducerDelegate)((clientAgnosticContext, cancellationToken) =>
                next.Invoke((ProducerContext<TNativeProperties>)clientAgnosticContext, cancellationToken));
            var resultingDelegate = clientAgnosticMiddleware(clientAgnosticDelegate);
            return resultingDelegate.Invoke(context, ct);
        },

        // Client-agnostic middleware
        (Type type, object[]) when typeof(IProducerMiddleware).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
        {
            var middleware = (IProducerMiddleware)context.ServiceProvider.GetRequiredService(type);
            var clientAgnosticDelegate = (ProducerDelegate)((clientAgnosticContext, cancellationToken) =>
                next.Invoke((ProducerContext<TNativeProperties>)clientAgnosticContext, cancellationToken));
            return middleware.InvokeAsync(context, clientAgnosticDelegate, ct);
        },

        // Convention based middleware
        (Type type, object[] args) => next =>
        {
            var methodInfos = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(mi => mi.Name == nameof(IProducerMiddleware.InvokeAsync))
                .ToArray();
            var methodInfo = (uint)methodInfos.Length switch
            {
                1 => methodInfos[0],
                0 => throw ProducerBuilderMiddlewareConventionException.NoInvokeAsyncMethod(type),
                > 1 => throw ProducerBuilderMiddlewareConventionException.AmbiguousInvokeAsyncMethods(type)
            };

            var parameters = methodInfo.GetParameters();
            if (parameters.Length < 2 ||
                !parameters.First().ParameterType.IsAssignableTo(typeof(ProducerContext)) ||
                parameters.Last().ParameterType != typeof(CancellationToken))
            {
                throw ProducerBuilderMiddlewareConventionException.MismatchParameters(type);
            }

            var contextParameterType = parameters[0].ParameterType;

            // Default client-agnostic middleware
            // e.g. class TMw { ctor(ProducerDelegate){} InvokeAsync(ProducerContext ...) }
            if (contextParameterType == typeof(ProducerContext))
            {
                var instance = MiddlewareCompiler.CreateInstance<ProducerDelegate>(type, args, serviceProvider,
                    (context, ct) => next((ProducerContext<TNativeProperties>)context, ct));
                if (parameters.Length == 1)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate>(instance).Invoke;
                }

                var factory = MiddlewareCompiler.Compile<ProducerContext<TNativeProperties>, Task>(methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // Default client-specific middleware
            // e.g. class TMw { ctor(ProducerDelegate<Props>){} InvokeAsync(ProducerContext<Props> ...) }
            if (contextParameterType == typeof(ProducerContext<TNativeProperties>))
            {
                var instance = MiddlewareCompiler.CreateInstance(type, args, serviceProvider, next);
                if (parameters.Length == 1)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TNativeProperties>>(instance);
                }

                var factory = MiddlewareCompiler.Compile<ProducerContext<TNativeProperties>, Task>(methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // Generic client-agnostic middleware
            // e.g. class TMw { ctor(ProducerDelegate){} InvokeAsync<TProps>(ProducerContext<TProps> ... ) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ProducerContext<>) &&
                methodInfo.IsGenericMethod &&
                methodInfo.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var instance = MiddlewareCompiler.CreateInstance<ProducerDelegate>(type, args, serviceProvider,
                    (context, ct) => next((ProducerContext<TNativeProperties>)context, ct));
                if (parameters.Length == 1)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TNativeProperties>>(instance).Invoke;
                }

                var factory = MiddlewareCompiler.Compile<ProducerContext<TNativeProperties>, Task>(methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // Generic client-agnostic middleware class-based
            // e.g. class TMw<TProps> { ctor(ProducerDelegate<TProps>){} { InvokeAsync(ProducerContext<TProps> ... ) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ProducerContext<>) &&
                type.IsGenericType &&
                type.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var genericType = type.GetGenericTypeDefinition().MakeGenericType(typeof(TNativeProperties));
                var instance = MiddlewareCompiler.CreateInstance(genericType, args, serviceProvider, next);
                if (parameters.Length == 1)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TNativeProperties>>(instance).Invoke;
                }

                var factory = MiddlewareCompiler.Compile<ProducerContext<TNativeProperties>, Task>(methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // If we cannot recognize the middleware, we simply skip it
            return next;
        },
        _ => throw ProducerBuilderMiddlewareConventionException.NotSupported()
    };
}