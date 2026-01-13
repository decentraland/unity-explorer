using System;
using System.Threading.Tasks;

namespace SceneRuntime
{
    public interface IJavaScriptEngine : IDisposable
    {
        IDCLScriptObject Global { get; }

        void Execute(string code);

        ICompiledScript Compile(string code);

        object Evaluate(ICompiledScript script);

        object Evaluate(string expression);

        void AddHostObject(string itemName, object target);

        IRuntimeHeapInfo? GetRuntimeHeapInfo();

        string GetStackTrace();

        object CreatePromiseFromTask<T>(Task<T> task);

        object CreatePromiseFromTask(Task task);
    }

    public interface ICompiledScript { }
}
