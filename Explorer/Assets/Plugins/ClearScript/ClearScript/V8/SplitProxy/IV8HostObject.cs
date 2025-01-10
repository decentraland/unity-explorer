using System;

namespace Microsoft.ClearScript.V8.SplitProxy
{
    public interface IV8HostObject
    {
        void GetNamedProperty(StdString name, V8Value value, out bool isConst) =>
            throw new NotImplementedException($"Named property {name.ToString()} is not implemented");

        void SetNamedProperty(StdString name, V8Value.Decoded value) =>
            throw new NotImplementedException($"Named property {name.ToString()} is not implemented");

        bool DeleteNamedProperty(StdString name) =>
            throw new NotImplementedException($"Named property {name.ToString()} is not implemented");

        void GetIndexedProperty(int index, V8Value value) =>
            throw new NotImplementedException($"Indexed property {index} is not implemented");

        void SetIndexedProperty(int index, V8Value.Decoded value) =>
            throw new NotImplementedException($"Indexed property {index} is not implemented");

        bool DeleteIndexedProperty(int index) =>
            throw new NotImplementedException($"Indexed property {index} is not implemented");

        void GetEnumerator(V8Value result) =>
            throw new NotImplementedException("Enumerator is not implemented");

        void GetAsyncEnumerator(V8Value result) =>
            throw new NotImplementedException("Async enumerator is not implemented");

        void GetNamedPropertyNames(StdStringArray names) =>
            throw new NotImplementedException("Listing named properties is not implemented");

        void GetIndexedPropertyIndices(StdInt32Array indices) =>
            throw new NotImplementedException("Listing indexed properties is not implemented");

        void InvokeMethod(StdString name, ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            GetNamedProperty(name, result, out _);
            var decoded = result.Decode();
            result.SetNonexistent();
            
            if (decoded.Type != V8Value.Type.HostObject)
                throw new InvalidCastException(
                    $"Tried to invoke property {name.ToString()}, but it returned a {decoded.GetTypeName()}");
            
            object method = decoded.GetHostObject();
            ((InvokeHostObject)method)(args, result);
        }

        static InvokeHostObject EmptyMethod = (args, result) => { };
    }

    public delegate void InvokeHostObject(ReadOnlySpan<V8Value.Decoded> args, V8Value result);
}