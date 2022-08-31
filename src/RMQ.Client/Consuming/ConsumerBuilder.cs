using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Consuming;
using RMQ.Client.Abstractions.Exceptions;
using RMQ.Client.Connection;

namespace RMQ.Client.Consuming;

internal class ConsumerBuilder : IConsumerBuilder
{
    private readonly IList<object> middlewares = new List<object>();
    private readonly IServiceProvider serviceProvider;

    public ConsumerBuilder(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public IConsumerBuilder With(Func<ConsumerDelegate, ConsumerDelegate> middleware)
    {
        middlewares.Add(middleware);
        return this;
    }

    public IConsumerBuilder With<TNativeProperties>(
        Func<ConsumerDelegate<TNativeProperties>, ConsumerDelegate<TNativeProperties>> middleware)
    {
        middlewares.Add(middleware);
        return this;
    }

    public IConsumerBuilder With<TKey, TMessage, TNativeProperties>(
        Func<ConsumerDelegate<TKey, TMessage, TNativeProperties>, ConsumerDelegate<TKey, TMessage, TNativeProperties>>
            middleware)
    {
        middlewares.Add(middleware);
        return this;
    }

    public IConsumerBuilder WithMiddleware(Type middlewareType, params object[] args)
    {
        middlewares.Add((middlewareType, args));
        return this;
    }

    public IConsumerBuilder Flush()
    {
        middlewares.Clear();
        return this;
    }

    public IConsumer BuildRabbit<TMessage, TProcessor>(RabbitConsumerParameters parameters)
        where TProcessor : IProcessor<string, TMessage>
    {
        var components = middlewares.Select(ToPipelineStep<string, TMessage, BasicDeliverEventArgs>).ToArray();

        var channelPool = serviceProvider.GetRequiredService<IConsumerChannelPool>();
        var logger = serviceProvider.GetService<ILogger<RabbitConsumer<TMessage, TProcessor>>>();

        return new RabbitConsumer<TMessage, TProcessor>(channelPool, serviceProvider, parameters, logger, components);
    }

    private Func<ConsumerDelegate<TKey, TMessage, TNativeProperties>,
            ConsumerDelegate<TKey, TMessage, TNativeProperties>>
        ToPipelineStep<TKey, TMessage, TNativeProperties>(object description)
    {
        return description switch
        {
            // Lambda specific middleware
            Func<ConsumerDelegate<TKey, TMessage, TNativeProperties>,
                    ConsumerDelegate<TKey, TMessage, TNativeProperties>>
                middleware => middleware,

            // Lambda message-agnostic middleware
            Func<ConsumerDelegate<TNativeProperties>, ConsumerDelegate<TNativeProperties>> messageAgnosticMiddleware =>
                next => (context, ct) =>
                {
                    var messageAgnosticDelegate =
                        (ConsumerDelegate<TNativeProperties>) ((messageAgnosticContext, token) =>
                            next.Invoke((ConsumerContext<TKey, TMessage, TNativeProperties>) messageAgnosticContext,
                                token));
                    var resultingDelegate = messageAgnosticMiddleware.Invoke(messageAgnosticDelegate);
                    return resultingDelegate.Invoke(context, ct);
                },

            // Lambda client-agnostic middleware
            Func<ConsumerDelegate, ConsumerDelegate> clientAgnosticMiddleware => next => (context, ct) =>
            {
                var clientAgnosticDelegate = (ConsumerDelegate) ((clientAgnosticContext, token) =>
                    next.Invoke((ConsumerContext<TKey, TMessage, TNativeProperties>) clientAgnosticContext, token));
                var resultingDelegate = clientAgnosticMiddleware.Invoke(clientAgnosticDelegate);
                return resultingDelegate.Invoke(context, ct);
            },

            // Interface specific middleware
            (Type type, object[]) when typeof(IConsumerMiddleware<TKey, TMessage, TNativeProperties>).GetTypeInfo()
                .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
            {
                var middleware = (IConsumerMiddleware<TKey, TMessage, TNativeProperties>) context.ServiceProvider
                    .GetRequiredService(type);
                return middleware.InvokeAsync(context, next, ct);
            },

            // Wild magic is going on here:
            // When we see the generic type that has a single IConsumerMiddleware<,,> interface
            // we try to find the matching sequence of TKey, TMessage and TNativeProperties combinations
            // that will result in given middleware class to implement IConsumerMiddleware<TKey, TMessage, TProps>.
            // By doing that we enable any declaration of generic middleware with an interface, such as
            // class Mw<TMessage> : IConsumerMiddleware<string, TMessage, Props>
            // class Mw<TProps, TMessage> : IConsumerMiddleware<long, TMessage, TProps> - the arguments are mixed, but it doesn't matter!
            // class Mw<TProps, TKey, TMessage> : IConsumerMiddleware<TKey, TMessage, TProps> - event that crazy stuff works
            (Type {IsGenericType: true} type, object[]) when
                type.GetInterfaces().FirstOrDefault()?.GetGenericTypeDefinition() == typeof(IConsumerMiddleware<,,>) &&
                TryMatchGenericType<TKey, TMessage, TNativeProperties>(type) is
                    {success: true, genericType: var genericType} =>
                next => (context, ct) =>
                {
                    var middleware = (IConsumerMiddleware<TKey, TMessage, TNativeProperties>) context.ServiceProvider
                        .GetRequiredService(genericType);
                    return middleware.InvokeAsync(context, next, ct);
                },

            // Interface message-agnostic middleware
            (Type type, object[]) when typeof(IConsumerMiddleware<TNativeProperties>).GetTypeInfo()
                .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
            {
                var middleware =
                    (IConsumerMiddleware<TNativeProperties>) context.ServiceProvider.GetRequiredService(type);
                var messageAgnosticDelegate = (ConsumerDelegate<TNativeProperties>) ((messageAgnosticContext, token) =>
                    next.Invoke((ConsumerContext<TKey, TMessage, TNativeProperties>) messageAgnosticContext, token));
                return middleware.InvokeAsync(context, messageAgnosticDelegate, ct);
            },

            // Interface client-agnostic middleware
            (Type type, object[]) when typeof(IConsumerMiddleware).GetTypeInfo()
                .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
            {
                // Interface typed middleware
                var middleware = (IConsumerMiddleware) context.ServiceProvider.GetRequiredService(type);
                var clientAgnosticDelegate = (ConsumerDelegate) ((clientAgnosticContext, token) =>
                    next.Invoke((ConsumerContext<TKey, TMessage, TNativeProperties>) clientAgnosticContext, token));
                return middleware.InvokeAsync(context, clientAgnosticDelegate, ct);
            },

            // Convention-based middleware
            (Type type, object[] args) => next =>
            {
                var methodInfos = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(mi => mi.Name == nameof(IConsumerMiddleware.InvokeAsync))
                    .ToArray();
                var methodInfo = (uint) methodInfos.Length switch
                {
                    1 => methodInfos[0],
                    0 => throw ConsumerBuilderMiddlewareConventionException.NoInvokeAsyncMethod(type),
                    > 1 => throw ConsumerBuilderMiddlewareConventionException.AmbiguousInvokeAsyncMethods(type)
                };

                var parameters = methodInfo.GetParameters();
                if (parameters.Length < 2 ||
                    !parameters.First().ParameterType.IsAssignableTo(typeof(ConsumerContext)) ||
                    parameters.Last().ParameterType != typeof(CancellationToken))
                {
                    throw ConsumerBuilderMiddlewareConventionException.MismatchParameters(type);
                }

                var contextParameterType = parameters[0].ParameterType;
                if (!contextParameterType.IsAssignableTo(typeof(ConsumerContext)))
                {
                    throw ConsumerBuilderMiddlewareConventionException.MismatchParameters(type);
                }

                // Default client-agnostic middleware
                // e.g. class TMw { ctor(ConsumerDelegate){} InvokeAsync(ConsumerContext ...) }
                if (contextParameterType == typeof(ConsumerContext))
                {
                    var instance = MiddlewareCompiler.CreateInstance<ConsumerDelegate>(type, args, serviceProvider,
                        (context, ct) => next.Invoke((ConsumerContext<TKey, TMessage, TNativeProperties>) context, ct));
                    if (parameters.Length == 2)
                    {
                        return methodInfo.CreateDelegate<ConsumerDelegate>(instance).Invoke;
                    }

                    var factory =
                        MiddlewareCompiler
                            .Compile<ConsumerContext<TKey, TMessage, TNativeProperties>, Task<ProcessResult>>(
                                methodInfo, parameters);
                    return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
                }

                // Default message-agnostic middleware
                // e.g. class TMw { ctor(ConsumerDelegate<Props>){} InvokeAsync(ConsumerContext<Props> ...)
                if (contextParameterType == typeof(ConsumerContext<TNativeProperties>))
                {
                    var instance = MiddlewareCompiler.CreateInstance<ConsumerDelegate<TNativeProperties>>(type, args,
                        serviceProvider,
                        (context, ct) => next.Invoke((ConsumerContext<TKey, TMessage, TNativeProperties>) context, ct));
                    if (parameters.Length == 2)
                    {
                        return methodInfo.CreateDelegate<ConsumerDelegate<TNativeProperties>>(instance).Invoke;
                    }

                    var factory =
                        MiddlewareCompiler
                            .Compile<ConsumerContext<TKey, TMessage, TNativeProperties>, Task<ProcessResult>>(
                                methodInfo, parameters);
                    return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
                }

                // Default specific middleware
                // e.g. class TMw { ctor(ConsumerDelegate<Props, Message>){} InvokeAsync(ConsumerContext<Props, Message> ...) }
                if (contextParameterType == typeof(ConsumerContext<TKey, TMessage, TNativeProperties>))
                {
                    var instance = MiddlewareCompiler.CreateInstance(type, args, serviceProvider, next);
                    if (parameters.Length == 2)
                    {
                        return methodInfo.CreateDelegate<ConsumerDelegate<TKey, TMessage, TNativeProperties>>(instance)
                            .Invoke;
                    }

                    var factory =
                        MiddlewareCompiler
                            .Compile<ConsumerContext<TKey, TMessage, TNativeProperties>, Task<ProcessResult>>(
                                methodInfo, parameters);
                    return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
                }

                // Generic client-agnostic middleware
                // e.g. class TMw { ctor(ConsumerDelegate){} InvokeAsync<TProps, TMessage>(ConsumerContext<TProps, TMessage> ...) }
                if (contextParameterType.GetGenericTypeDefinition() == typeof(ConsumerContext<,,>) &&
                    methodInfo.IsGenericMethod &&
                    methodInfo.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
                {
                    var instance = MiddlewareCompiler.CreateInstance<ConsumerDelegate>(type, args, serviceProvider,
                        (context, ct) => next.Invoke((ConsumerContext<TKey, TMessage, TNativeProperties>) context, ct));
                    if (parameters.Length == 2)
                    {
                        return methodInfo.CreateDelegate<ConsumerDelegate<TKey, TMessage, TNativeProperties>>(instance)
                            .Invoke;
                    }

                    var factory =
                        MiddlewareCompiler
                            .Compile<ConsumerContext<TKey, TMessage, TNativeProperties>, Task<ProcessResult>>(
                                methodInfo, parameters);
                    return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
                }

                // Generic client-agnostic middleware class-based
                // e.g. class TMw<TProps, TMessage> { ctor(ConsumerDelegate<TProps, TMessage>){} { InvokeAsync(ConsumerContext<TProps, TMessage> ... ) }
                if (contextParameterType.GetGenericTypeDefinition() == typeof(ConsumerContext<,,>) &&
                    type.IsGenericType &&
                    type.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
                {
                    var genericType = type.GetGenericTypeDefinition()
                        .MakeGenericType(typeof(TKey), typeof(TMessage), typeof(TNativeProperties));
                    var instance = MiddlewareCompiler.CreateInstance(genericType, args, serviceProvider, next);
                    if (parameters.Length == 2)
                    {
                        return methodInfo.CreateDelegate<ConsumerDelegate<TKey, TMessage, TNativeProperties>>(instance)
                            .Invoke;
                    }

                    var factory =
                        MiddlewareCompiler
                            .Compile<ConsumerContext<TKey, TMessage, TNativeProperties>, Task<ProcessResult>>(
                                methodInfo, parameters);
                    return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
                }

                // if we cannot recognise the middleware, we simply skip it
                return next;
            },
            _ => throw new NotSupportedException()
        };
    }

    /// <summary>
    /// Given
    /// class MyMiddleware{TKey} : IConsumerMiddleware{TKey, string, Props}
    /// for consumer {long, string, Props}
    /// make generic type MyMiddleware{long},
    ///
    /// Given
    /// class MyMiddleware{TProps, TMessage} : IConsumerMiddleware{string, TMessage, TProps}
    /// for consumer {string, MyMessage, Props}
    /// make generic type MyMiddleware{Props, MyMessage}
    /// </summary>
    /// <param name="middlewareType"></param>
    /// <returns></returns>
    private static (bool success, Type genericType) TryMatchGenericType<TKey, TMessage, TNativeProperties>(
        Type middlewareType)
    {
        var genericArguments = new[] {typeof(TKey), typeof(TMessage), typeof(TNativeProperties)};
        var combinations = genericArguments.GetCombinations(middlewareType.GetGenericArguments().Length);

        var genericType = combinations
            .Select(c => middlewareType.MakeGenericType(c.ToArray()))
            .FirstOrDefault(g => g.IsAssignableTo(typeof(IConsumerMiddleware<TKey, TMessage, TNativeProperties>)));
        return genericType is null
            ? (false, middlewareType)
            : (true, genericType);
    }
}