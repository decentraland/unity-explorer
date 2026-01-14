using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.Threading.Tasks;

namespace SceneRuntime.V8
{
    public class V8JavaScriptEngineAdapter : IJavaScriptEngine
    {
        public IDCLScriptObject Global => new V8ScriptObjectAdapter(V8Engine.Global);

        public V8ScriptEngine V8Engine { get; }

        public V8JavaScriptEngineAdapter(V8ScriptEngine engine)
        {
            this.V8Engine = engine;
        }

        public void Dispose() =>
            V8Engine.Dispose();

        public void Execute(string code) =>
            V8Engine.Execute(code);

        public ICompiledScript Compile(string code)
        {
            V8Script script = V8Engine.Compile(code);
            return new V8CompiledScriptAdapter(script);
        }

        public object Evaluate(ICompiledScript script)
        {
            if (script is V8CompiledScriptAdapter adapter)
                return V8Engine.Evaluate(adapter.V8Script);

            throw new ArgumentException("Script must be a V8CompiledScriptAdapter", nameof(script));
        }

        public object Evaluate(string expression) =>
            V8Engine.Evaluate(expression);

        public void AddHostObject(string itemName, object target) =>
            V8Engine.AddHostObject(itemName, target);

        public IRuntimeHeapInfo? GetRuntimeHeapInfo()
        {
            V8RuntimeHeapInfo heapInfo = V8Engine.GetRuntimeHeapInfo();
            return heapInfo != null ? new V8RuntimeHeapInfoAdapter(heapInfo) : null;
        }

        public string GetStackTrace() =>
            V8Engine.GetStackTrace();

        public object CreatePromiseFromTask<T>(Task<T> task) =>
            task.ToPromise()!;

        public object CreatePromiseFromTask(Task task) =>
            task.ToPromise()!;
    }
}
