using System;

namespace Utility
{
    /// <summary>
    ///     Marks a method as per-frame/per-call hot: DCLA003 enforces the same
    ///     allocation-freedom on its body as on system Update() methods. Apply to
    ///     code invoked at frame rate or per network/URL operation outside ECS systems.
    ///     Methods only: the analyzer does not inspect constructors, so allowing the
    ///     attribute there would create unchecked (false-safety) annotations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HotPathAttribute : Attribute { }
}
