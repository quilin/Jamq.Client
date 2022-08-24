using System.Linq.Expressions;
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
            .IsAssignableFrom(type.GetTypeInfo()) => next => context =>
        {
            var middleware = (IProducerMiddleware<TNativeProperties>)context.ServiceProvider.GetRequiredService(type);
            return middleware.InvokeAsync(context, next);
        },

        // Lambda client-agnostic middleware
        Func<ProducerDelegate, ProducerDelegate> clientAgnosticMiddleware => next => context =>
        {
            var clientAgnosticDelegate = (ProducerDelegate)(clientAgnosticContext =>
                next.Invoke(ProducerContext<TNativeProperties>.From(clientAgnosticContext)));
            var resultingDelegate = clientAgnosticMiddleware(clientAgnosticDelegate);
            return resultingDelegate.Invoke(context);
        },

        // Client-agnostic middleware
        (Type type, object[]) when typeof(IProducerMiddleware).GetTypeInfo()
            .IsAssignableFrom(type.GetTypeInfo()) => next => context =>
        {
            var middleware = (IProducerMiddleware)context.ServiceProvider.GetRequiredService(type);
            var clientAgnosticDelegate = (ProducerDelegate)(clientAgnosticContext =>
                next.Invoke(ProducerContext<TNativeProperties>.From(clientAgnosticContext)));
            return middleware.InvokeAsync(context, clientAgnosticDelegate);
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
            if (parameters.Length == 0)
            {
                throw ProducerBuilderMiddlewareConventionException.ContextParameterMismatch(type);
            }

            var contextParameterType = parameters[0].ParameterType;

            // Default client-agnostic middleware
            // e.g. class TMw { ctor(ProducerDelegate){} InvokeAsync(ProducerContext ...) }
            if (contextParameterType == typeof(ProducerContext))
            {
                var instance = GetMiddlewareInstance<ProducerDelegate>(type, args, serviceProvider,
                    context => next(ProducerContext<TNativeProperties>.From(context)));
                if (parameters.Length == 1)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate>(instance).Invoke;
                }

                var factory = Compile<ProducerContext<TNativeProperties>>(methodInfo, parameters);
                return context => factory(instance, context, context.ServiceProvider);
            }

            // Default client-specific middleware
            // e.g. class TMw { ctor(ProducerDelegate<Props>){} InvokeAsync(ProducerContext<Props> ...) }
            if (contextParameterType == typeof(ProducerContext<TNativeProperties>))
            {
                var instance = GetMiddlewareInstance(type, args, serviceProvider, next);
                if (parameters.Length == 1)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TNativeProperties>>(instance);
                }

                var factory = Compile<ProducerContext<TNativeProperties>>(methodInfo, parameters);
                return context => factory(instance, context, context.ServiceProvider);
            }

            // Generic client-agnostic middleware
            // e.g. class TMw { ctor(ProducerDelegate){} InvokeAsync<TProps>(ProducerContext<TProps> ... ) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ProducerContext<>) &&
                methodInfo.IsGenericMethod &&
                methodInfo.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var instance = GetMiddlewareInstance<ProducerDelegate>(type, args, serviceProvider,
                    context => next(ProducerContext<TNativeProperties>.From(context)));
                if (parameters.Length == 1)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TNativeProperties>>(instance).Invoke;
                }

                var factory = Compile<ProducerContext<TNativeProperties>>(methodInfo, parameters);
                return context => factory(instance, context, context.ServiceProvider);
            }

            // Generic client-agnostic middleware class-based
            // e.g. class TMw<TProps> { ctor(ProducerDelegate<TProps>){} { InvokeAsync(ProducerContext<TProps> ... ) }
            if (contextParameterType.GetGenericTypeDefinition() == typeof(ProducerContext<>) &&
                type.IsGenericType &&
                type.GetGenericArguments().SequenceEqual(contextParameterType.GetGenericArguments()))
            {
                var genericType = type.GetGenericTypeDefinition().MakeGenericType(typeof(TNativeProperties));
                var instance = GetMiddlewareInstance(genericType, args, serviceProvider, next);
                if (parameters.Length == 1)
                {
                    return methodInfo.CreateDelegate<ProducerDelegate<TNativeProperties>>(instance).Invoke;
                }

                var factory = Compile<ProducerContext<TNativeProperties>>(methodInfo, parameters);
                return context => factory(instance, context, context.ServiceProvider);
            }

            // If we cannot recognize the middleware, we simply skip it
            return next;
        },
        _ => throw ProducerBuilderMiddlewareConventionException.NotSupported()
    };

    private static object GetMiddlewareInstance<TProducerDelegate>(
        Type type, object[] args, IServiceProvider serviceProvider, TProducerDelegate producerDelegate)
    {
        var constructorArgs = new object[args.Length + 1];
        constructorArgs[0] = producerDelegate!;
        Array.Copy(args, 0, constructorArgs, 1, args.Length);
        return ActivatorUtilities.CreateInstance(serviceProvider, type, constructorArgs);
    }

    /// <summary>
    /// Replace
    /// <remarks>
    /// public class Middleware
    /// {
    ///     public Task InvokeAsync(ProducerContext context, IDependency dependency)
    /// }
    /// </remarks>
    /// with
    /// <remarks>
    /// Task InvokeAsync(Middleware instance, ProducerContext context, IServiceProvider provider)
    /// {
    ///     return instance.InvokeAsync(context, (IDependency) ProducerBuilder.GetService(provider, typeof(IDependency));
    /// }
    /// </remarks>
    /// </summary>
    private static Func<object, TContext, IServiceProvider, Task> Compile<TContext>(
        MethodInfo methodInfo, IReadOnlyList<ParameterInfo> parameters)
    {
        var nativePropertiesType = typeof(TContext).GetGenericArguments().Single();

        var producerContextArg = Expression.Parameter(typeof(TContext), "producerContext");
        var providerArg = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        var instanceArg = Expression.Parameter(typeof(object), "middleware");

        var methodArguments = new Expression[parameters.Count];
        methodArguments[0] = producerContextArg;
        for (var i = 1; i < parameters.Count; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
            {
                throw ProducerBuilderMiddlewareConventionException.NotSupported();
            }

            var parameterTypeExpression = new Expression[]
            {
                providerArg,
                Expression.Constant(parameterType, typeof(Type))
            };

            var getServiceCall = Expression.Call(GetServiceInfo, parameterTypeExpression);
            methodArguments[i] = Expression.Convert(getServiceCall, parameterType);
        }

        Expression middlewareInstanceArg = instanceArg;
        if (methodInfo.DeclaringType is not null && methodInfo.DeclaringType != typeof(object))
        {
            var declaringType = methodInfo.DeclaringType.IsGenericTypeDefinition
                ? methodInfo.DeclaringType.GetGenericTypeDefinition().MakeGenericType(nativePropertiesType)
                : methodInfo.DeclaringType;
            middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, declaringType);
        }

        var body = methodInfo switch
        {
            // InvokeAsync<TProps>
            {IsGenericMethod: true} =>
                Expression.Call(middlewareInstanceArg, methodInfo.Name, new[] {nativePropertiesType},
                    methodArguments),
            // class TMw<TProps> { InvokeAsync }
            {ContainsGenericParameters: true} =>
                Expression.Call(middlewareInstanceArg, methodInfo.Name, Type.EmptyTypes, methodArguments),
            _ =>
                Expression.Call(middlewareInstanceArg, methodInfo, methodArguments)
        };
        var lambda = Expression.Lambda<Func<object, TContext, IServiceProvider, Task>>(
            body, instanceArg, producerContextArg, providerArg);

        return lambda.Compile();
    }

    private static object GetService(IServiceProvider serviceProvider, Type type) =>
        serviceProvider.GetRequiredService(type);

    private static readonly MethodInfo GetServiceInfo = typeof(ProducerBuilder)
        .GetMethod(nameof(GetService), BindingFlags.NonPublic | BindingFlags.Static)!;
}