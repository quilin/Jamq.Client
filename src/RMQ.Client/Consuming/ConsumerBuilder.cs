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

    public IConsumerBuilder With<TNativeProperties, TMessage>(
        Func<ConsumerDelegate<TNativeProperties, TMessage>, ConsumerDelegate<TNativeProperties, TMessage>> middleware)
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

    public IConsumer BuildRabbit<TProcessor, TMessage>(RabbitConsumerParameters parameters)
        where TProcessor : IProcessor<TMessage>
    {
        var components = middlewares.Select(ToPipelineStep<BasicDeliverEventArgs, TMessage>);

        var channelPool = serviceProvider.GetRequiredService<IConsumerChannelPool>();
        var logger = serviceProvider.GetService<ILogger<Consumer<TMessage, TProcessor>>>();

        return new Consumer<TMessage, TProcessor>(channelPool, serviceProvider, parameters, logger, components);
    }

    private Func<ConsumerDelegate<TNativeProperties, TMessage>, ConsumerDelegate<TNativeProperties, TMessage>>
        ToPipelineStep<TNativeProperties, TMessage>(object description) => description switch
    {
        // Lambda client-specific middleware
        Func<ConsumerDelegate<TNativeProperties, TMessage>, ConsumerDelegate<TNativeProperties, TMessage>> middleware =>
            middleware,

        // Client-specific middleware
        (Type type, object[]) when typeof(IConsumerMiddleware<TNativeProperties>).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, cancellationToken) =>
        {
            var middleware = (IConsumerMiddleware<TNativeProperties>) context.ServiceProvider.GetRequiredService(type);
            return middleware.InvokeAsync(context, next, cancellationToken);
        },

        // Lambda client-agnostic middleware
        Func<ConsumerDelegate, ConsumerDelegate> clientAgnosticMiddleware => next => (context, ct) =>
        {
            var clientAgnosticDelegate = (ConsumerDelegate) ((clientAgnosticContext, cancellationToken) =>
                next.Invoke((ConsumerContext<TNativeProperties, TMessage>) clientAgnosticContext, cancellationToken));
            var resultingDelegate = clientAgnosticMiddleware(clientAgnosticDelegate);
            return resultingDelegate(context, ct);
        },

        // Client-agnostic middleware
        (Type type, object[]) when typeof(IConsumerMiddleware).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
        {
            // Interface typed middleware
            var middleware = (IConsumerMiddleware) context.ServiceProvider.GetRequiredService(type);
            var clientAgnosticDelegate = (ConsumerDelegate) ((clientAgnosticContext, cancellationToken) =>
                next.Invoke((ConsumerContext<TNativeProperties, TMessage>) clientAgnosticContext, cancellationToken));
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
                var instance = GetMiddlewareInstance<ConsumerDelegate>(type, args, serviceProvider,
                    (context, ct) => next((ConsumerContext<TNativeProperties, TMessage>) context, ct));
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ConsumerDelegate>(instance).Invoke;
                }

                var factory =
                    MiddlewareCompiler.Compile<ConsumerContext<TNativeProperties, TMessage>, Task<ProcessResult>>(
                        methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // Default client-specific middleware
            // e.g. class TMw { ctor(ConsumerDelegate<Props, Message>){} InvokeAsync(ConsumerContext<Props, Message> ...) }
            if (contextParameterType == typeof(ConsumerContext<TNativeProperties, TMessage>))
            {
                var instance = GetMiddlewareInstance(type, args, serviceProvider, next);
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ConsumerDelegate<TNativeProperties, TMessage>>(instance).Invoke;
                }

                var factory =
                    MiddlewareCompiler.Compile<ConsumerContext<TNativeProperties, TMessage>, Task<ProcessResult>>(
                        methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }
            
            // Generic client-agnostic middleware
            // e.g. class TMw { ctor(ConsumerDelegate){} InvokeAsync<TProps, TMessage>(ConsumerContext<TProps, TMessage> ...) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ConsumerContext<,>) &&
                methodInfo.IsGenericMethod &&
                methodInfo.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var instance = GetMiddlewareInstance<ConsumerDelegate>(type, args, serviceProvider,
                    (context, ct) => next((ConsumerContext<TNativeProperties, TMessage>) context, ct));
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ConsumerDelegate<TNativeProperties, TMessage>>(instance).Invoke;
                }

                var factory =
                    MiddlewareCompiler.Compile<ConsumerContext<TNativeProperties, TMessage>, Task<ProcessResult>>(
                        methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }
            
            // Generic client-agnostic middleware class-based
            // e.g. class TMw<TProps, TMessage> { ctor(ConsumerDelegate<TProps, TMessage>){} { InvokeAsync(ConsumerContext<TProps, TMessage> ... ) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ConsumerContext<,>) &&
                type.IsGenericType &&
                type.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var genericType = type.GetGenericTypeDefinition()
                    .MakeGenericType(typeof(TNativeProperties), typeof(TMessage));
                var instance = GetMiddlewareInstance(genericType, args, serviceProvider, next);
                if (parameters.Length == 2)
                {
                    return methodInfo.CreateDelegate<ConsumerDelegate<TNativeProperties, TMessage>>(instance).Invoke;
                }

                var factory =
                    MiddlewareCompiler.Compile<ConsumerContext<TNativeProperties, TMessage>, Task<ProcessResult>>(
                        methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            }

            // if we cannot recognise the middleware, we simply skip it
            return next;
        },
        _ => throw new NotSupportedException()
    };

    private static object GetMiddlewareInstance<TConsumerDelegate>(
        Type type, object[] args, IServiceProvider serviceProvider, TConsumerDelegate consumerDelegate)
    {
        var constructorArgs = new object[args.Length + 1];
        constructorArgs[0] = consumerDelegate!;
        Array.Copy(args, 0, constructorArgs, 1, args.Length);
        return ActivatorUtilities.CreateInstance(serviceProvider, type, constructorArgs);
    }
}