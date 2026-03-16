using System;
using Cysharp.Threading.Tasks;

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

        object CreatePromise<T>(UniTask<T> uniTask);

        object CreatePromise(UniTask uniTask);
    }

    public interface ICompiledScript { }
}
