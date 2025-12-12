using System.Reflection;

namespace Jamq.Client;

internal static class MethodInfoExtensions
{
    public static TResult CreateDelegate<TResult>(this MethodInfo methodInfo, object instance)
        where TResult : Delegate => (TResult)methodInfo.CreateDelegate(typeof(TResult), instance);
}