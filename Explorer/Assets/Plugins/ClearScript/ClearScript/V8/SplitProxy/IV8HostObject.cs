using System;

namespace Microsoft.ClearScript.V8.SplitProxy
{
    public interface IV8HostObject
    {
        void GetNamedProperty(StdString name, V8Value value, out bool isConst);
        void SetNamedProperty(StdString name, V8Value.Decoded value);
        bool DeleteNamedProperty(StdString name) => false;

        void GetIndexedProperty(int index, V8Value pValue);
        void SetIndexedProperty(int index, V8Value.Decoded value);
        bool DeleteIndexedProperty(int index) => false;

        void GetEnumerator(V8Value result) => result.SetNonexistent();
        void GetAsyncEnumerator(V8Value result) => result.SetNonexistent();

        void GetNamedPropertyNames(StdStringArray names) => names.SetElementCount(0);
        void GetIndexedPropertyIndices(StdInt32Array indices) => indices.SetElementCount(0);

        void InvokeMethod(StdString name, ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            GetNamedProperty(name, result, out _);
            object method = result.GetHostObject();
            ((InvokeHostObject)method)(args, result);
        }
    }
    
    public delegate void InvokeHostObject(ReadOnlySpan<V8Value.Decoded> args, V8Value result);
}