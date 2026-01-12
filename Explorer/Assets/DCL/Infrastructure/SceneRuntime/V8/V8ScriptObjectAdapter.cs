using Microsoft.ClearScript;
using System;
using Utility;
using IScriptObject = SceneRuntime.IScriptObject;

namespace DLC.SceneRuntime.V8
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
        public void SetProperty(int index, object value) => scriptObject.SetProperty(index, value);

        public IScriptObject Invoke(bool asConstructor, params object[] args)
        {
            object result = scriptObject.Invoke(asConstructor, args);
            if (result is ScriptObject so)
                return new V8ScriptObjectAdapter(so);

            //TODO FRAN: CHECK THIS -> Possible 'System.InvalidCastException'
            return new V8ScriptObjectAdapter((ScriptObject)result);
        }

        public void SetProperty(int index, IDCLTypedArray<byte> value)
        {
            throw new NotImplementedException();
        }

        public IDCLTypedArray<byte> InvokeMethod(string subarray, int i, int dataOffset) =>
            throw new NotImplementedException();

        public ScriptObject ScriptObject => scriptObject;

        public static implicit operator ScriptObject(V8ScriptObjectAdapter adapter) => adapter.scriptObject;
    }
}
