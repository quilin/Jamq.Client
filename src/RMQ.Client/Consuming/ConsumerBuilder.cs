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

    public IConsumerBuilder With<TMessage, TNativeProperties>(
        Func<ConsumerDelegate<TMessage, TNativeProperties>, ConsumerDelegate<TMessage, TNativeProperties>> middleware)
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
        where TProcessor : IProcessor<TMessage>
    {
        var components = middlewares.Select(ToPipelineStep<TMessage, BasicDeliverEventArgs>);

        var channelPool = serviceProvider.GetRequiredService<IConsumerChannelPool>();
        var logger = serviceProvider.GetService<ILogger<Consumer<TMessage, TProcessor>>>();

        return new Consumer<TMessage, TProcessor>(channelPool, serviceProvider, parameters, logger, components);
    }

    private Func<ConsumerDelegate<TMessage, TNativeProperties>, ConsumerDelegate<TMessage, TNativeProperties>>
        ToPipelineStep<TMessage, TNativeProperties>(object description) => description switch
    {
        // Lambda specific middleware
        Func<ConsumerDelegate<TMessage, TNativeProperties>, ConsumerDelegate<TMessage, TNativeProperties>> middleware =>
            middleware,

        // Lambda message-agnostic middleware
        Func<ConsumerDelegate<TNativeProperties>, ConsumerDelegate<TNativeProperties>> messageAgnosticMiddleware =>
            next => (context, ct) =>
            {
                var messageAgnosticDelegate = (ConsumerDelegate<TNativeProperties>)((messageAgnosticContext, token) =>
                    next.Invoke((ConsumerContext<TMessage, TNativeProperties>)messageAgnosticContext, token));
                var resultingDelegate = messageAgnosticMiddleware.Invoke(messageAgnosticDelegate);
                return resultingDelegate.Invoke(context, ct);
            },

        // Lambda client-agnostic middleware
        Func<ConsumerDelegate, ConsumerDelegate> clientAgnosticMiddleware => next => (context, ct) =>
        {
            var clientAgnosticDelegate = (ConsumerDelegate)((clientAgnosticContext, token) =>
                next.Invoke((ConsumerContext<TMessage, TNativeProperties>)clientAgnosticContext, token));
            var resultingDelegate = clientAgnosticMiddleware.Invoke(clientAgnosticDelegate);
            return resultingDelegate.Invoke(context, ct);
        },

        // Interface specific middleware
        (Type type, object[]) when typeof(IConsumerMiddleware<TMessage, TNativeProperties>).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
        {
            var middleware = (IConsumerMiddleware<TMessage, TNativeProperties>)context.ServiceProvider
                .GetRequiredService(type);
            return middleware.InvokeAsync(context, next, ct);
        },

        // Interface message-generic middleware
        // e.g. class TMw<TMessage> : IConsumerMiddleware<Props, TMessage> {}
        (Type { IsGenericType: true } type, object[])
            when type.GetInterfaces().Any(i =>
                i.GetGenericTypeDefinition() == typeof(IConsumerMiddleware<,>) &&
                i.GetGenericArguments().SequenceEqual(
                    new[] { type.GetGenericArguments().First(), typeof(TNativeProperties) }))
            => next => (context, ct) =>
            {
                var genericType = type.GetGenericTypeDefinition().MakeGenericType(typeof(TMessage));
                var middleware = (IConsumerMiddleware<TMessage, TNativeProperties>)context.ServiceProvider
                    .GetRequiredService(genericType);
                return middleware.InvokeAsync(context, next, ct);
            },

        // Interface generic middleware
        // e.g. class TMw<TProps, TMessage> : IConsumerMiddleware<TProps, TMessage> {}
        (Type { IsGenericType: true } type, object[])
            when type.GetInterfaces().Any(i =>
                i.GetGenericTypeDefinition() == typeof(IConsumerMiddleware<,>) &&
                i.GetGenericArguments().SequenceEqual(type.GetGenericArguments()))
            => next => (context, ct) =>
            {
                var genericType = type.GetGenericTypeDefinition().MakeGenericType(
                    typeof(TMessage), typeof(TNativeProperties));
                var middleware = (IConsumerMiddleware<TMessage, TNativeProperties>)context.ServiceProvider
                    .GetRequiredService(genericType);
                return middleware.InvokeAsync(context, next, ct);
            },

        // Interface message-agnostic middleware
        (Type type, object[]) when typeof(IConsumerMiddleware<TNativeProperties>).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
        {
            var middleware = (IConsumerMiddleware<TNativeProperties>)context.ServiceProvider.GetRequiredService(type);
            var messageAgnosticDelegate = (ConsumerDelegate<TNativeProperties>)((messageAgnosticContext, token) =>
                next.Invoke((ConsumerContext<TMessage, TNativeProperties>)messageAgnosticContext, token));
            return middleware.InvokeAsync(context, messageAgnosticDelegate, ct);
        },

        // Interface client-agnostic middleware
        (Type type, object[]) when typeof(IConsumerMiddleware).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
        {
            // Interface typed middleware
            var middleware = (IConsumerMiddleware)context.ServiceProvider.GetRequiredService(type);
            var clientAgnosticDelegate = (ConsumerDelegate)((clientAgnosticContext, token) =>
                next.Invoke((ConsumerContext<TMessage, TNativeProperties>)clientAgnosticContext, token));
            return middleware.InvokeAsync(context, clientAgnosticDelegate, ct);
        },

        // Convention-based middleware
        (Type type, object[] args) => next =>
        {
            var methodInfos = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(mi => mi.Name == nameof(IConsumerMiddleware.InvokeAsync))
                .ToArray();
            var methodInfo = (uint)methodInfos.Length switch
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
                    (context, ct) => next.Invoke((ConsumerContext<TMessage, TNativeProperties>)context, ct));
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ConsumerDelegate>(instance).Invoke;
                }

                var factory =
                    MiddlewareCompiler.Compile<ConsumerContext<TMessage, TNativeProperties>, Task<ProcessResult>>(
                        methodInfo, parameters);
                return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
            }

            // Default message-agnostic middleware
            // e.g. class TMw { ctor(ConsumerDelegate<Props>){} InvokeAsync(ConsumerContext<Props> ...)
            if (contextParameterType == typeof(ConsumerContext<TNativeProperties>))
            {
                var instance = MiddlewareCompiler.CreateInstance<ConsumerDelegate<TNativeProperties>>(type, args, serviceProvider,
                    (context, ct) => next.Invoke((ConsumerContext<TMessage, TNativeProperties>)context, ct));
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ConsumerDelegate<TNativeProperties>>(instance).Invoke;
                }

                var factory =
                    MiddlewareCompiler.Compile<ConsumerContext<TMessage, TNativeProperties>, Task<ProcessResult>>(
                        methodInfo, parameters);
                return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
            }

            // Default specific middleware
            // e.g. class TMw { ctor(ConsumerDelegate<Props, Message>){} InvokeAsync(ConsumerContext<Props, Message> ...) }
            if (contextParameterType == typeof(ConsumerContext<TMessage, TNativeProperties>))
            {
                var instance = MiddlewareCompiler.CreateInstance(type, args, serviceProvider, next);
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ConsumerDelegate<TMessage, TNativeProperties>>(instance).Invoke;
                }

                var factory =
                    MiddlewareCompiler.Compile<ConsumerContext<TMessage, TNativeProperties>, Task<ProcessResult>>(
                        methodInfo, parameters);
                return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
            }

            // Generic client-agnostic middleware
            // e.g. class TMw { ctor(ConsumerDelegate){} InvokeAsync<TProps, TMessage>(ConsumerContext<TProps, TMessage> ...) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ConsumerContext<,>) &&
                methodInfo.IsGenericMethod &&
                methodInfo.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var instance = MiddlewareCompiler.CreateInstance<ConsumerDelegate>(type, args, serviceProvider,
                    (context, ct) => next.Invoke((ConsumerContext<TMessage, TNativeProperties>)context, ct));
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ConsumerDelegate<TMessage, TNativeProperties>>(instance).Invoke;
                }

                var factory =
                    MiddlewareCompiler.Compile<ConsumerContext<TMessage, TNativeProperties>, Task<ProcessResult>>(
                        methodInfo, parameters);
                return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
            }

            // Generic client-agnostic middleware class-based
            // e.g. class TMw<TProps, TMessage> { ctor(ConsumerDelegate<TProps, TMessage>){} { InvokeAsync(ConsumerContext<TProps, TMessage> ... ) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ConsumerContext<,>) &&
                type.IsGenericType &&
                type.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var genericType = type.GetGenericTypeDefinition()
                    .MakeGenericType(typeof(TMessage), typeof(TNativeProperties));
                var instance = MiddlewareCompiler.CreateInstance(genericType, args, serviceProvider, next);
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ConsumerDelegate<TMessage, TNativeProperties>>(instance).Invoke;
                }

                var factory =
                    MiddlewareCompiler.Compile<ConsumerContext<TMessage, TNativeProperties>, Task<ProcessResult>>(
                        methodInfo, parameters);
                return (context, ct) => factory.Invoke(instance, context, context.ServiceProvider, ct);
            }

            // if we cannot recognise the middleware, we simply skip it
            return next;
        },
        _ => throw new NotSupportedException()
    };
}