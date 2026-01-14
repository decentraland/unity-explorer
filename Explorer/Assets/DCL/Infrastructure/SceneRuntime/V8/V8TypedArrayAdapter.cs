using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using Utility;

namespace SceneRuntime.V8
{
    public class V8TypedArrayAdapter : IDCLTypedArray<byte>, IDCLScriptObject
    {
        public ITypedArray<byte> TypedArray { get; }

        public ScriptObject ScriptObject { get; }

        ulong IDCLTypedArray<byte>.Length => TypedArray.Length;

        ulong IDCLTypedArray<byte>.Size => TypedArray.Size;

        IDCLArrayBuffer IDCLTypedArray<byte>.ArrayBuffer
        {
            get
            {
                IArrayBuffer arrayBuffer = TypedArray.ArrayBuffer;
                return new V8ArrayBufferAdapter(arrayBuffer);
            }
        }

        IEnumerable<string> IDCLScriptObject.PropertyNames => ScriptObject.PropertyNames;

        object IDCLScriptObject.this[string name, params object[] args]
        {
            get => ScriptObject[name, args];
            set => ScriptObject[name, args] = value;
        }

        public V8TypedArrayAdapter(ITypedArray<byte> typedArray)
        {
            TypedArray = typedArray;
            ScriptObject = (ScriptObject)typedArray;
        }

        public static implicit operator ScriptObject(V8TypedArrayAdapter adapter) =>
            adapter.ScriptObject;

        ulong IDCLTypedArray<byte>.Read(ulong index, ulong length, byte[] destination, ulong destinationIndex) =>
            TypedArray.Read(index, length, destination, destinationIndex);

        void IDCLTypedArray<byte>.InvokeWithDirectAccess(Action<IntPtr> action) =>
            TypedArray.InvokeWithDirectAccess(action);

        int IDCLTypedArray<byte>.InvokeWithDirectAccess(Func<IntPtr, int> func) =>
            TypedArray.InvokeWithDirectAccess(func);

        void IDCLTypedArray<byte>.ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex) =>
            TypedArray.Read(offset, count, destination, destinationIndex);

        void IDCLTypedArray<byte>.WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
        {
            if (count == 0)
                return;

            TypedArray.InvokeWithDirectAccess(pData =>
            {
                unsafe
                {
                    byte* destPtr = (byte*)pData.ToPointer() + offset;

                    fixed (byte* srcPtr = source)
                    {
                        byte* src = srcPtr + sourceIndex;
                        Buffer.MemoryCopy(src, destPtr, count, count);
                    }
                }
            });
        }

        object IDCLScriptObject.GetProperty(string name, params object[] args) =>
            ScriptObject.GetProperty(name, args);

        void IDCLScriptObject.SetProperty(string name, params object[] args) =>
            ScriptObject.SetProperty(name, args);

        void IDCLScriptObject.SetProperty(int index, object value) =>
            ScriptObject.SetProperty(index, value);

        //TODO FRAN: Check this logic
        object IDCLScriptObject.Invoke(bool asConstructor, params object[] args)
        {
            object result = ScriptObject.Invoke(asConstructor, args);

            if (result is ITypedArray<byte> ta)
                return new V8TypedArrayAdapter(ta);

            if (result is ScriptObject so)
                return new V8ScriptObjectAdapter(so);

            return result;
        }

        //TODO FRAN: Check this logic
        object IDCLScriptObject.InvokeMethod(string name, params object[] args)
        {
            object result = ScriptObject.InvokeMethod(name, args);

            if (result is ITypedArray<byte> ta) { return new V8TypedArrayAdapter(ta); }

            if (result is not ScriptObject so) return result;

            try
            {
                var typedArray = (ITypedArray<byte>)so;
                return new V8TypedArrayAdapter(typedArray);
            }
            catch { return new V8ScriptObjectAdapter(so); }
        }

        object IDCLScriptObject.InvokeAsFunction(params object[] args) =>
            ScriptObject.InvokeAsFunction(args);

        /// <inheritdoc />
        object IDCLScriptObject.GetNativeObject() =>
            ScriptObject;
    }
}
