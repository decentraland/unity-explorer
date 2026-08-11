using System;

namespace Utility
{
    /// <summary>
    ///     Marks a method or constructor as per-frame/per-call hot: DCLA003 enforces the
    ///     same allocation-freedom on its body as on system Update() methods. Apply to
    ///     code invoked at frame rate or per network/URL operation outside ECS systems.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public sealed class HotPathAttribute : Attribute { }
}
