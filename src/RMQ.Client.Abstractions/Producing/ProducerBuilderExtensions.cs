using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace RMQ.Client.Abstractions.Producing;

/// <summary>
/// Extensions for typed middleware configuration
/// </summary>
public static class ProducerBuilderExtensions
{
    /// <summary>
    /// Add middleware of given type to the pipeline
    /// </summary>
    /// <param name="builder">Producer builder</param>
    /// <param name="args">Middleware constructor arguments</param>
    /// <typeparam name="TMiddleware">Middleware type</typeparam>
    /// <returns>Builder itself for chaining</returns>
    public static IProducerBuilder WithMiddleware<TMiddleware>(
        this IProducerBuilder builder, params object[] args) =>
        WithMiddleware(builder, typeof(TMiddleware), args);

    /// <summary>
    /// Add middleware of given type to the pipeline
    /// </summary>
    /// <param name="builder">Producer builder</param>
    /// <param name="middlewareType">Middleware type</param>
    /// <param name="args">Middleware constructor arguments</param>
    /// <returns>Builder itself for chaining</returns>
    public static IProducerBuilder WithMiddleware(
        this IProducerBuilder builder, Type middlewareType, params object[] args)
    {
        if (typeof(IProducerMiddleware).GetTypeInfo().IsAssignableFrom(middlewareType.GetTypeInfo()))
        {
            return builder.With(next => context =>
            {
                var middleware = (IProducerMiddleware)context.ServiceProvider.GetRequiredService(middlewareType);
                return middleware.InvokeAsync(context, next);
            });
        }

        return builder.With(next =>
        {
            var methodInfo = middlewareType.GetMethod(
                nameof(IProducerMiddleware.InvokeAsync), BindingFlags.Instance | BindingFlags.Public);

            if (methodInfo is null)
            {
                throw new InvalidOperationException($"Middleware has to have a method {nameof(IProducerMiddleware.InvokeAsync)}");
            }

            var parameters = methodInfo.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(ProducerContext))
            {
                throw new InvalidOperationException($"Middleware method {nameof(IProducerMiddleware.InvokeAsync)} has to have first parameter of type {nameof(ProducerContext)}");
            }

            var constructorArgs = new object[args.Length + 1];
            constructorArgs[0] = next;
            Array.Copy(args, 0, constructorArgs, 1, args.Length);
            var instance = ActivatorUtilities.CreateInstance(builder.ServiceProvider, middlewareType, constructorArgs);

            if (parameters.Length == 1)
            {
                var producerDelegate = methodInfo.CreateDelegate(typeof(ProducerDelegate), instance);
                return (ProducerDelegate)producerDelegate;
            }

            var factory = Compile<object>(methodInfo, parameters);
            return context =>
            {
                var services = context.ServiceProvider;
                return factory(instance, context, services);
            };
        });
    }

    private static Func<TMiddleware, ProducerContext, IServiceProvider, Task> Compile<TMiddleware>(
        MethodInfo methodInfo, IReadOnlyList<ParameterInfo> parameters)
    {
        var middleware = typeof(TMiddleware);

        var producerContextArg = Expression.Parameter(typeof(ProducerContext), "producerContext");
        var providerArg = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        var instanceArg = Expression.Parameter(middleware, "middleware");

        var methodArguments = new Expression[parameters.Count];
        methodArguments[0] = producerContextArg;
        for (var i = 1; i < parameters.Count; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
            {
                throw new NotSupportedException();
            }

            var parameterTypeExpression = new Expression[]
            {
                providerArg,
                Expression.Constant(parameters, typeof(Type))
            };

            var getServiceCall = Expression.Call(GetServiceInfo, parameterTypeExpression);
            methodArguments[i] = Expression.Convert(getServiceCall, parameterType);
        }

        Expression middlewareInstanceArg = instanceArg;
        if (methodInfo.DeclaringType is not null && methodInfo.DeclaringType != typeof(TMiddleware))
        {
            middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, methodInfo.DeclaringType);
        }

        var body = Expression.Call(middlewareInstanceArg, methodInfo, methodArguments);
        var lambda = Expression.Lambda<Func<TMiddleware, ProducerContext, IServiceProvider, Task>>(
            body, instanceArg, producerContextArg, providerArg);

        return lambda.Compile();
    }

    private static object GetService(IServiceProvider serviceProvider, Type type) =>
        serviceProvider.GetRequiredService(type);

    private static readonly MethodInfo GetServiceInfo = typeof(ProducerBuilderExtensions)
        .GetMethod(nameof(GetService), BindingFlags.NonPublic | BindingFlags.Static)!;
}