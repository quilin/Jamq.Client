using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Jamq.Client.Abstractions.Exceptions;
using Jamq.Client.Abstractions.Producing;

namespace Jamq.Client.Producing;

internal class ProducerBuilder : IProducerBuilder
{
    private readonly IList<object> middlewares = new List<object>();

    public ProducerBuilder(
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
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

    public IProducerBuilder With<TKey, TMessage, TNativeProperties>(
        Func<ProducerDelegate<TKey, TMessage, TNativeProperties>, ProducerDelegate<TKey, TMessage, TNativeProperties>>
            middleware)
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

    IServiceProvider IProducerBuilder.GetServiceProvider() => ServiceProvider;

    IEnumerable<Func<ProducerDelegate<TKey, TMessage, TProperties>, ProducerDelegate<TKey, TMessage, TProperties>>>
        IProducerBuilder.GetMiddlewares<TKey, TMessage, TProperties>() =>
        middlewares.Select(ToPipelineStep<TKey, TMessage, TProperties>);

    private Func<ProducerDelegate<TKey, TMessage, TNativeProperties>,
            ProducerDelegate<TKey, TMessage, TNativeProperties>>
        ToPipelineStep<TKey, TMessage, TNativeProperties>(object description) => description switch
    {
        // Lambda specific middleware
        Func<ProducerDelegate<TKey, TMessage, TNativeProperties>, ProducerDelegate<TKey, TMessage, TNativeProperties>>
            middleware => middleware,

        // Lambda message-agnostic middleware
        Func<ProducerDelegate<TNativeProperties>, ProducerDelegate<TNativeProperties>> messageAgnosticMiddleware =>
            next => (context, ct) =>
            {
                var messageAgnosticDelegate = (ProducerDelegate<TNativeProperties>)((messageAgnosticContext, token) =>
                    next.Invoke((ProducerContext<TKey, TMessage, TNativeProperties>)messageAgnosticContext, token));
                var resultingDelegate = messageAgnosticMiddleware.Invoke(messageAgnosticDelegate);
                return resultingDelegate.Invoke(context, ct);
            },

        // Lambda client-agnostic middleware
        Func<ProducerDelegate, ProducerDelegate> clientAgnosticMiddleware => next => (context, ct) =>
        {
            var clientAgnosticDelegate = (ProducerDelegate)((clientAgnosticContext, token) =>
                next.Invoke((ProducerContext<TKey, TMessage, TNativeProperties>)clientAgnosticContext, token));
            var resultingDelegate = clientAgnosticMiddleware.Invoke(clientAgnosticDelegate);
            return resultingDelegate.Invoke(context, ct);
        },

        // Interface specific middleware
        (Type type, object[]) when typeof(IProducerMiddleware<TKey, TMessage, TNativeProperties>).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
        {
            var middleware = (IProducerMiddleware<TKey, TMessage, TNativeProperties>)context.ServiceProvider
                .GetRequiredService(type);
            return middleware.InvokeAsync(context, next, ct);
        },

        // Wild magic is going on here:
        // When we see the generic type that has a single IProducerMiddleware<,,> interface
        // we try to find the matching sequence of TKey, TMessage and TNativeProperties combinations
        // that will result in given middleware class to implement IProducerMiddleware<TKey, TMessage, TProps>.
        // By doing that we enable any declaration of generic middleware with an interface, such as
        // class Mw<TMessage> : IProducerMiddleware<string, TMessage, Props>
        // class Mw<TProps, TMessage> : IProducerMiddleware<long, TMessage, TProps> - the arguments are mixed, but it doesn't matter!
        // class Mw<TProps, TKey, TMessage> : IProducerMiddleware<TKey, TMessage, TProps> - event that crazy stuff works
        (Type { IsGenericType: true } type, object[]) when
            MiddlewareCompiler.TryMatchGenericInterface<IProducerMiddleware<TKey, TMessage, TNativeProperties>>(type) is
                { Success: true, GenericType: var genericType } =>
            next => (context, ct) =>
            {
                var middleware = (IProducerMiddleware<TKey, TMessage, TNativeProperties>)context.ServiceProvider
                    .GetRequiredService(genericType);
                return middleware.InvokeAsync(context, next, ct);
            },

        // Interface message-agnostic middleware
        (Type type, object[]) when typeof(IProducerMiddleware<TNativeProperties>).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
        {
            var middleware = (IProducerMiddleware<TNativeProperties>)context.ServiceProvider.GetRequiredService(type);
            var messageAgnosticDelegate = (ProducerDelegate<TNativeProperties>)((messageAgnosticContext, token) =>
                next.Invoke((ProducerContext<TKey, TMessage, TNativeProperties>)messageAgnosticContext, token));
            return middleware.InvokeAsync(context, messageAgnosticDelegate, ct);
        },

        // Interface client-agnostic middleware
        (Type type, object[]) when typeof(IProducerMiddleware).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
        {
            var middleware = (IProducerMiddleware)context.ServiceProvider.GetRequiredService(type);
            var clientAgnosticDelegate = (ProducerDelegate)((clientAgnosticContext, cancellationToken) =>
                next.Invoke((ProducerContext<TKey, TMessage, TNativeProperties>)clientAgnosticContext,
                    cancellationToken));
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
                var instance = MiddlewareCompiler.CreateInstance<ProducerDelegate>(type, args, ServiceProvider,
                    (context, ct) => next((ProducerContext<TKey, TMessage, TNativeProperties>)context, ct));
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate>(instance).Invoke;
                }

                var factory = MiddlewareCompiler
                    .Compile<ProducerContext<TKey, TMessage, TNativeProperties>, Task>(methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // Default message-agnostic middleware
            // e.g. class TMw { ctor(ProducerDelegate<Props>){} InvokeAsync(ProducerContext<Props> ...) }
            if (contextParameterType == typeof(ProducerContext<TNativeProperties>))
            {
                var instance = MiddlewareCompiler.CreateInstance<ProducerDelegate<TNativeProperties>>(type, args,
                    ServiceProvider,
                    (context, ct) => next.Invoke((ProducerContext<TKey, TMessage, TNativeProperties>)context, ct));
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TNativeProperties>>(instance).Invoke;
                }

                var factory = MiddlewareCompiler
                    .Compile<ProducerContext<TKey, TMessage, TNativeProperties>, Task>(methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // Default specific middleware
            // e.g. class TMw { ctor(ProducerDelegate<Key, Message, Props>){} InvokeAsync(ProducerContext<Key, Message, Props> ...) }
            if (contextParameterType == typeof(ProducerContext<TKey, TMessage, TNativeProperties>))
            {
                var instance = MiddlewareCompiler.CreateInstance(type, args, ServiceProvider, next);
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TKey, TMessage, TNativeProperties>>(instance)
                        .Invoke;
                }

                var factory = MiddlewareCompiler
                    .Compile<ProducerContext<TKey, TMessage, TNativeProperties>, Task>(methodInfo, parameters);
                return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
            }

            // Generic client-agnostic middleware
            // e.g. class TMw { ctor(ProducerDelegate){} InvokeAsync<TProps>(ProducerContext<TProps> ... ) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ProducerContext<>) &&
                methodInfo.IsGenericMethod &&
                methodInfo.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var instance = MiddlewareCompiler.CreateInstance<ProducerDelegate>(type, args, ServiceProvider,
                    (context, ct) => next((ProducerContext<TKey, TMessage, TNativeProperties>)context, ct));
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TNativeProperties>>(instance).Invoke;
                }

                var factory = MiddlewareCompiler
                    .Compile<ProducerContext<TNativeProperties>, Task>(methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // Generic message-agnostic middleware class-based
            // e.g. class TMw<TProps> { ctor(ProducerDelegate<TProps>){} { InvokeAsync(ProducerContext<TProps> ... ) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ProducerContext<>) &&
                type.IsGenericType &&
                type.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var genericType = type.GetGenericTypeDefinition().MakeGenericType(typeof(TNativeProperties));
                var messageAgnosticDelegate = (ProducerDelegate<TNativeProperties>)((context, ct) =>
                    next.Invoke((ProducerContext<TKey, TMessage, TNativeProperties>)context, ct));
                var instance =
                    MiddlewareCompiler.CreateInstance(genericType, args, ServiceProvider, messageAgnosticDelegate);
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TNativeProperties>>(instance).Invoke;
                }

                var factory = MiddlewareCompiler
                    .Compile<ProducerContext<TNativeProperties>, Task>(methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // If we cannot recognize the middleware, we simply skip it
            return next;
        },
        _ => throw ProducerBuilderMiddlewareConventionException.NotSupported()
    };

    IServiceProvider ServiceProvider { get; }
}