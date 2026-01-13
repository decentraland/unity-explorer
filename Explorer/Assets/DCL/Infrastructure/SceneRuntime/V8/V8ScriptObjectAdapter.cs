using Microsoft.ClearScript;
using System.Collections.Generic;

namespace SceneRuntime.V8
{
    public class V8ScriptObjectAdapter : IDCLScriptObject
    {
        public V8ScriptObjectAdapter(ScriptObject scriptObject)
        {
            this.ScriptObject = scriptObject;
        }

        public ScriptObject ScriptObject { get; }

        /// <inheritdoc/>
        public object InvokeMethod(string name, params object[] args) => ScriptObject.InvokeMethod(name, args);

        /// <inheritdoc/>
        public object InvokeAsFunction(params object[] args) => ScriptObject.InvokeAsFunction(args);

        /// <inheritdoc/>
        public object GetProperty(string name, params object[] args) => ScriptObject.GetProperty(name, args);

        /// <inheritdoc/>
        public void SetProperty(string name, params object[] args) => ScriptObject.SetProperty(name, args);

        public IEnumerable<string> PropertyNames => ScriptObject.PropertyNames;

        public object this[string name, params object[] args]
        {
            get => ScriptObject[name, args];
            set => ScriptObject[name, args] = value;
        }

        /// <inheritdoc/>
        public void SetProperty(int index, object value) => ScriptObject.SetProperty(index, value);

        /// <inheritdoc/>
        public object Invoke(bool asConstructor, params object[] args) => ScriptObject.Invoke(asConstructor, args);

        public static implicit operator ScriptObject(V8ScriptObjectAdapter adapter) => adapter.ScriptObject;
    }
}
