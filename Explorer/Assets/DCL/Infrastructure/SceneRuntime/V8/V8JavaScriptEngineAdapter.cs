using DLC.SceneRuntime.V8;
using Microsoft.ClearScript.V8;
using System;

namespace SceneRuntime.V8
{
    public class V8JavaScriptEngineAdapter : IJavaScriptEngine
    {
        private readonly V8ScriptEngine engine;

        public V8JavaScriptEngineAdapter(V8ScriptEngine engine)
        {
            this.engine = engine;
        }

        public void Execute(string code) => engine.Execute(code);

        public ICompiledScript Compile(string code)
        {
            V8Script script = engine.Compile(code);
            return new V8CompiledScriptAdapter(script);
        }

        public object Evaluate(ICompiledScript script)
        {
            if (script is V8CompiledScriptAdapter adapter)
                return engine.Evaluate(adapter.V8Script);
            throw new ArgumentException("Script must be a V8CompiledScriptAdapter", nameof(script));
        }

        public object Evaluate(string expression) => engine.Evaluate(expression);

        public void AddHostObject(string itemName, object target) => engine.AddHostObject(itemName, target);

        public IScriptObject Global => new V8ScriptObjectAdapter(engine.Global);

        public IRuntimeHeapInfo? GetRuntimeHeapInfo()
        {
            V8RuntimeHeapInfo? heapInfo = engine.GetRuntimeHeapInfo();
            return heapInfo != null ? new V8RuntimeHeapInfoAdapter(heapInfo) : null;
        }

        public string GetStackTrace() => engine.GetStackTrace();

        public void Dispose() => engine.Dispose();

        public V8ScriptEngine V8Engine => engine;
    }
}
