using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Rusty.Space.Product.Engine.Tests;

/// <summary>
/// Binds only the calls a test records. New SDK members require no stub; an
/// unexpected call fails rather than returning a default success or fake handle.
/// </summary>
public class RecordingServiceProxy : DispatchProxy
{
    private Func<MethodInfo, object?[]?, object?> dispatch = null!;

    internal static T For<T>(object recorder) where T : class => Create<T>((method, arguments) =>
    {
        Type[] parameters = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        MethodInfo recorded = recorder.GetType().GetMethod(method.Name, parameters)
            ?? throw new NotSupportedException($"Unrecorded Engine call: {typeof(T).Name}.{method.Name}");
        try
        {
            return recorded.Invoke(recorder, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    });

    internal static T Create<T>(Func<MethodInfo, object?[]?, object?> dispatch) where T : class
    {
        T service = DispatchProxy.Create<T, RecordingServiceProxy>();
        ((RecordingServiceProxy)(object)service).dispatch = dispatch;
        return service;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        dispatch(targetMethod ?? throw new InvalidOperationException("Missing Engine method."), args);
}
