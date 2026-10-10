namespace OpenCvSharp.Internal;

/// <summary>
/// Marks a static method that is passed to native code as a callback.
/// AOT runtimes (Unity IL2CPP, Mono full AOT) recognize this attribute by its name and
/// generate a native-callable entry point for the method. It has no effect on other runtimes.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class MonoPInvokeCallbackAttribute : Attribute
{
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="delegateType">The delegate type the method is marshalled as.</param>
    public MonoPInvokeCallbackAttribute(Type delegateType)
    {
        DelegateType = delegateType;
    }

    /// <summary>
    /// The delegate type the method is marshalled as.
    /// </summary>
    public Type DelegateType { get; }
}
