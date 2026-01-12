using System;

namespace SceneRuntime
{
    public interface IJavaScriptEngine : IDisposable
    {
        void Execute(string code);
        ICompiledScript Compile(string code);
        object Evaluate(ICompiledScript script);
        object Evaluate(string expression);
        void AddHostObject(string itemName, object target);
        IScriptObject Global { get; }
        IRuntimeHeapInfo? GetRuntimeHeapInfo();
        string GetStackTrace();
    }

    public interface ICompiledScript
    {
    }
}
