using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RMQ.Client.Abstractions;
using RMQ.Client.Abstractions.Consuming;
using RMQ.Client.Connection;

namespace RMQ.Client.Consuming;

internal class ConsumerBuilder : IConsumerBuilder
{
    private readonly IList<object> middlewares = new List<object>();

    public ConsumerBuilder(
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public IConsumerBuilder With(Func<ConsumerDelegate, ConsumerDelegate> middleware)
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
        var components = middlewares.Select(ToPipelineStep<TMessage>);

        var channelPool = ServiceProvider.GetRequiredService<IConsumerChannelPool>();
        var logger = ServiceProvider.GetService<ILogger<Consumer<TMessage, TProcessor>>>();

        return new Consumer<TMessage, TProcessor>(channelPool, ServiceProvider, parameters, logger, components);
    }

    private Func<ConsumerDelegate<TMessage>, ConsumerDelegate<TMessage>> ToPipelineStep<TMessage>(object description) =>
        description switch
        {
            (Type type, object[]) when typeof(IConsumerMiddleware).GetTypeInfo()
                .IsAssignableFrom(type.GetTypeInfo()) => next => (context, ct) =>
            {
                var middleware = (IConsumerMiddleware)context.ServiceProvider.GetRequiredService(type);
                return middleware.InvokeAsync(context, next, ct);
            },
            (Type type, object[] args) => next =>
            {
                var methodInfo = type.GetMethod(nameof(IConsumerMiddleware.InvokeAsync),
                    BindingFlags.Instance | BindingFlags.Public);

                if (methodInfo is null)
                {
                    throw new InvalidOperationException();
                }

                var parameters = methodInfo.GetParameters();
                if (parameters.Length < 2 ||
                    parameters.Last().ParameterType != typeof(CancellationToken))
                {
                    throw new InvalidOperationException();
                }

                var constructorArgs = new object[args.Length + 1];
                constructorArgs[0] = next;
                Array.Copy(args, 0, constructorArgs, 1, args.Length);
                var instance = ActivatorUtilities.CreateInstance(ServiceProvider, type, constructorArgs);
                if (parameters.Length == 2)
                {
                    var consumerDelegate = methodInfo.CreateDelegate(typeof(ConsumerDelegate<TMessage>), instance);
                    return (ConsumerDelegate<TMessage>)consumerDelegate;
                }

                var factory = Compile<object, TMessage>(methodInfo, parameters);
                return (context, ct) => factory(instance, context, context.ServiceProvider, ct);
            },
            _ => throw new NotSupportedException()
        };

    private static Func<T, ConsumerContext<TMessage>, IServiceProvider, CancellationToken, Task<ProcessResult>>
        Compile<T, TMessage>(MethodInfo methodInfo, IReadOnlyList<ParameterInfo> parameters)
    {
        var middlewareType = typeof(T);

        var consumerContextArg = Expression.Parameter(typeof(ConsumerContext<TMessage>), "context");
        var providerArg = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        var cancellationTokenArg = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var instanceArg = Expression.Parameter(middlewareType, "middleware");

        var methodArguments = new Expression[parameters.Count];
        methodArguments[0] = consumerContextArg;
        methodArguments[parameters.Count - 1] = cancellationTokenArg;
        for (var i = 1; i < parameters.Count - 1; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
            {
                throw new NotSupportedException();
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
        if (methodInfo.DeclaringType is not null && methodInfo.DeclaringType != typeof(T))
        {
            middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, methodInfo.DeclaringType);
        }

        var body = Expression.Call(middlewareInstanceArg, methodInfo, methodArguments);
        var lambda = Expression.Lambda<Func<T, ConsumerContext<TMessage>, IServiceProvider, CancellationToken, Task<ProcessResult>>>(
            body, instanceArg, consumerContextArg, providerArg, cancellationTokenArg);

        return lambda.Compile();
    }

    private static object GetService(IServiceProvider serviceProvider, Type type) =>
        serviceProvider.GetRequiredService(type);

    private static readonly MethodInfo GetServiceInfo = typeof(ConsumerBuilder)
        .GetMethod(nameof(GetService), BindingFlags.NonPublic | BindingFlags.Static)!;

    public IServiceProvider ServiceProvider { get; }
}