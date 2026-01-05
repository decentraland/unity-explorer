using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;

namespace SceneRuntime
{
    public class V8ScriptObjectAdapter : IScriptObject
    {
        private readonly ScriptObject scriptObject;

        public V8ScriptObjectAdapter(ScriptObject scriptObject)
        {
            this.scriptObject = scriptObject;
        }

        public object InvokeAsFunction(params object[] args) => scriptObject.InvokeAsFunction(args);

        public object GetProperty(string name) => scriptObject.GetProperty(name);

        public void SetProperty(string name, object value) => scriptObject.SetProperty(name, value);

        public IScriptObject Invoke(bool asConstructor, params object[] args)
        {
            object result = scriptObject.Invoke(asConstructor, args);
            if (result is ScriptObject so)
                return new V8ScriptObjectAdapter(so);
            return new V8ScriptObjectAdapter((ScriptObject)result);
        }

        public ScriptObject ScriptObject => scriptObject;

        public static implicit operator ScriptObject(V8ScriptObjectAdapter adapter) => adapter.scriptObject;
    }

    public class V8CompiledScriptAdapter : ICompiledScript
    {
        private readonly Microsoft.ClearScript.V8.V8Script v8Script;

        public V8CompiledScriptAdapter(Microsoft.ClearScript.V8.V8Script v8Script)
        {
            this.v8Script = v8Script;
        }

        public Microsoft.ClearScript.V8.V8Script V8Script => v8Script;
    }
}
