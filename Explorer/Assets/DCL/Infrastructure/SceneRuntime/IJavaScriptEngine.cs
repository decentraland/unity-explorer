using Microsoft.ClearScript.JavaScript;
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

    public interface IScriptObject
    {
        object InvokeAsFunction(params object[] args);
        object GetProperty(string name);
        void SetProperty(string name, object value);
        IScriptObject Invoke(bool asConstructor, params object[] args);
    }
}
