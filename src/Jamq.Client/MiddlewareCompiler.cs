using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Jamq.Client.Abstractions.Exceptions;

namespace Jamq.Client;

internal static class MiddlewareCompiler
{
    /// <summary>
    /// Given
    /// class MyMiddleware{TKey} : IProducerMiddleware{TKey, string, Props}
    /// for consumer {long, string, Props}
    /// make generic type MyMiddleware{long},
    ///
    /// Given
    /// class MyMiddleware{TProps, TMessage} : IProducerMiddleware{string, TMessage, TProps}
    /// for consumer {string, MyMessage, Props}
    /// make generic type MyMiddleware{Props, MyMessage}
    /// </summary>
    /// <param name="middlewareType"></param>
    /// <returns></returns>
    public static (bool Success, Type GenericType) TryMatchGenericInterface<TMiddleware>(Type middlewareType)
    {
        var genericTypeDefinition = typeof(TMiddleware).GetGenericTypeDefinition();
        if (middlewareType.GetInterfaces().All(i => i.GetGenericTypeDefinition() != genericTypeDefinition))
        {
            return (false, middlewareType);
        }
        
        var genericArguments = typeof(TMiddleware).GetGenericArguments();
        var combinations = genericArguments.GetCombinations(middlewareType.GetGenericArguments().Length);

        var genericType = combinations
            .Select(c => middlewareType.MakeGenericType(c.ToArray()))
            .FirstOrDefault(typeof(TMiddleware).IsAssignableFrom);
        return genericType is null
            ? (false, middlewareType)
            : (true, genericType);
    }
    
    public static object CreateInstance<TDelegate>(
        Type type, object[] args, IServiceProvider serviceProvider, TDelegate delegateArgument)
    {
        var constructorArgs = new object[args.Length + 1];
        constructorArgs[0] = delegateArgument!;
        Array.Copy(args, 0, constructorArgs, 1, args.Length);
        return ActivatorUtilities.CreateInstance(serviceProvider, type, constructorArgs);
    }
    
    /// <summary>
    /// Replace
    /// <remarks>
    /// public class Middleware
    /// {
    ///     public Task InvokeAsync(TContext context, IDependency dependency)
    /// }
    /// </remarks>
    /// with
    /// <remarks>
    /// Task InvokeAsync(Middleware instance, TContext context, IServiceProvider provider)
    /// {
    ///     return instance.InvokeAsync(context, (IDependency) ProducerBuilder.GetService(provider, typeof(IDependency));
    /// }
    /// </remarks>
    /// </summary>
    public static Func<object, TContext, IServiceProvider, CancellationToken, TResult> Compile<TContext, TResult>(
            MethodInfo methodInfo, IReadOnlyList<ParameterInfo> parameters) where TResult : Task
    {
        var contextGenericArguments = typeof(TContext).GetGenericArguments();

        var consumerContextArg = Expression.Parameter(typeof(TContext), "context");
        var providerArg = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        var cancellationTokenArg = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var instanceArg = Expression.Parameter(typeof(object), "middleware");

        var methodArguments = new Expression[parameters.Count];
        methodArguments[0] = consumerContextArg;
        methodArguments[parameters.Count - 1] = cancellationTokenArg;
        for (var i = 1; i < parameters.Count - 1; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
            {
                throw ConsumerBuilderMiddlewareConventionException.NotSupported();
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
                ? methodInfo.DeclaringType.GetGenericTypeDefinition().MakeGenericType(contextGenericArguments)
                : methodInfo.DeclaringType;
            middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, declaringType);
        }

        var body = methodInfo switch
        {
            {IsGenericMethod: true} =>
                Expression.Call(middlewareInstanceArg, methodInfo.Name, contextGenericArguments, methodArguments),
            {ContainsGenericParameters: true} =>
                Expression.Call(middlewareInstanceArg, methodInfo.Name, Type.EmptyTypes, methodArguments),
            _ =>
                Expression.Call(middlewareInstanceArg, methodInfo, methodArguments)
        };
        var lambda =
            Expression.Lambda<Func<object, TContext, IServiceProvider, CancellationToken, TResult>>(
                body, instanceArg, consumerContextArg, providerArg, cancellationTokenArg);

        return lambda.Compile();
    }

    private static object GetService(IServiceProvider serviceProvider, Type type) =>
        serviceProvider.GetRequiredService(type);

    private static readonly MethodInfo GetServiceInfo = typeof(MiddlewareCompiler)
        .GetMethod(nameof(GetService), BindingFlags.NonPublic | BindingFlags.Static)!;
}