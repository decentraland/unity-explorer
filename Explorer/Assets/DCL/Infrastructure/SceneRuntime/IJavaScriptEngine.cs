using System;
using Cysharp.Threading.Tasks;

namespace SceneRuntime
{
    /// <summary>
    ///     Platform-agnostic interface for the JavaScript engine that executes scene code.
    ///     Implementations include the ClearScript/V8 desktop engine and the WebGL jslib-backed
    ///     <see cref="WebClient.WebClientJavaScriptEngine" />.
    ///     <para>
    ///         A single engine instance maps to one scene context. Host objects (C# instances callable from JS)
    ///         are registered via <see cref="AddHostObject" />. Scripts can be compiled once with
    ///         <see cref="Compile" /> and evaluated repeatedly with <see cref="Evaluate(ICompiledScript)" /> to
    ///         avoid re-parsing overhead. UniTask-backed promises are bridged through
    ///         <see cref="CreatePromise{T}" /> / <see cref="CreatePromise" />.
    ///     </para>
    /// </summary>
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

    /// <summary>
    ///     Marker interface for a pre-compiled script returned by <see cref="IJavaScriptEngine.Compile" />.
    ///     The concrete type is engine-specific (e.g., <see cref="WebClient.WebGLCompiledScript" /> on WebGL).
    /// </summary>
    public interface ICompiledScript { }
}
