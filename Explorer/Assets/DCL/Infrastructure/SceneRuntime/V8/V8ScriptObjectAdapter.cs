using Microsoft.ClearScript;
using System.Collections.Generic;

namespace SceneRuntime.V8
{
    public class V8ScriptObjectAdapter : IDCLScriptObject
    {
        public ScriptObject ScriptObject { get; }

        public IEnumerable<string> PropertyNames => ScriptObject.PropertyNames;

        public object this[string name, params object[] args]
        {
            get => ScriptObject[name, args];
            set => ScriptObject[name, args] = value;
        }

        public V8ScriptObjectAdapter(ScriptObject scriptObject)
        {
            ScriptObject = scriptObject;
        }

        /// <inheritdoc />
        public object InvokeMethod(string name, params object[] args) =>
            ScriptObject.InvokeMethod(name, args);

        /// <inheritdoc />
        public object InvokeAsFunction(params object[] args) =>
            ScriptObject.InvokeAsFunction(args);

        /// <inheritdoc />
        public object GetProperty(string name, params object[] args) =>
            ScriptObject.GetProperty(name, args);

        /// <inheritdoc />
        public void SetProperty(string name, params object[] args) =>
            ScriptObject.SetProperty(name, args);

        //TODO FRAN: Check this logic
        /// <inheritdoc />
        public void SetProperty(int index, object value)
        {
            // Convert adapters to ScriptObject so they're enumerable in JavaScript
            // ClearScript can handle ScriptObject natively but not adapter wrappers
            object valueToSet = value;

            if (value is V8ScriptObjectAdapter v8Adapter)
                valueToSet = v8Adapter.ScriptObject;
            else if (value is V8TypedArrayAdapter v8TypedAdapter)
                valueToSet = v8TypedAdapter.ScriptObject;

            ScriptObject.SetProperty(index, valueToSet);
        }

        /// <inheritdoc />
        public object Invoke(bool asConstructor, params object[] args) =>
            ScriptObject.Invoke(asConstructor, args);

        /// <inheritdoc />
        public object GetNativeObject() =>
            ScriptObject;

        public static implicit operator ScriptObject(V8ScriptObjectAdapter adapter) =>
            adapter.ScriptObject;
    }
}
