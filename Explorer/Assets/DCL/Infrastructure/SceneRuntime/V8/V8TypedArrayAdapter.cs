using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.IO;
using Utility;

namespace SceneRuntime.V8
{
    public class V8TypedArrayAdapter : IDCLTypedArray<byte>, IDCLScriptObject
    {
        private readonly ITypedArray<byte> typedArray;
        private readonly ScriptObject scriptObject;

        public V8TypedArrayAdapter(ITypedArray<byte> typedArray)
        {
            this.typedArray = typedArray;
            this.scriptObject = (ScriptObject)typedArray;
        }

        public ITypedArray<byte> TypedArray => typedArray;
        public ScriptObject ScriptObject => scriptObject;

        public static implicit operator ScriptObject(V8TypedArrayAdapter adapter) => adapter.scriptObject;

        ulong IDCLTypedArray<byte>.Length => typedArray.Length;

        ulong IDCLTypedArray<byte>.Size => typedArray.Size;

        IDCLArrayBuffer IDCLTypedArray<byte>.ArrayBuffer
        {
            get
            {
                IArrayBuffer arrayBuffer = typedArray.ArrayBuffer;
                return new V8ArrayBufferAdapter(arrayBuffer);
            }
        }

        ulong IDCLTypedArray<byte>.Read(ulong index, ulong length, byte[] destination, ulong destinationIndex) =>
            typedArray.Read(index, length, destination, destinationIndex);

        void IDCLTypedArray<byte>.InvokeWithDirectAccess(Action<IntPtr> action) =>
            typedArray.InvokeWithDirectAccess(action);

        int IDCLTypedArray<byte>.InvokeWithDirectAccess(Func<IntPtr, int> func) => typedArray.InvokeWithDirectAccess(func);

        void IDCLTypedArray<byte>.ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex) =>
            typedArray.Read(offset, count, destination, destinationIndex);

        void IDCLTypedArray<byte>.WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
        {
            if (count == 0)
                return;

            typedArray.InvokeWithDirectAccess(pData =>
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
            scriptObject.GetProperty(name, args);

        void IDCLScriptObject.SetProperty(string name, params object[] args) =>
            scriptObject.SetProperty(name, args);

        IEnumerable<string> IDCLScriptObject.PropertyNames => scriptObject.PropertyNames;

        object IDCLScriptObject.this[string name, params object[] args]
        {
            get => scriptObject[name, args];
            set => scriptObject[name, args] = value;
        }

        void IDCLScriptObject.SetProperty(int index, object value) =>
            scriptObject.SetProperty(index, value);

        //TODO FRAN: Check this logic
        object IDCLScriptObject.Invoke(bool asConstructor, params object[] args)
        {
            object result = scriptObject.Invoke(asConstructor, args);
            if (result is ITypedArray<byte> ta)
                return new V8TypedArrayAdapter(ta);
            if (result is ScriptObject so)
                return new V8ScriptObjectAdapter(so);
            return result;
        }

        //TODO FRAN: Check this logic
        object IDCLScriptObject.InvokeMethod(string name, params object[] args)
        {
            object result = scriptObject.InvokeMethod(name, args);

            if (result is ITypedArray<byte> ta)
            {
                return new V8TypedArrayAdapter(ta);
            }

            if (result is not ScriptObject so) return result;

            try
            {
                ITypedArray<byte> typedArray = (ITypedArray<byte>)so;
                return new V8TypedArrayAdapter(typedArray);
            }
            catch
            {
                return new V8ScriptObjectAdapter(so);
            }
        }

        object IDCLScriptObject.InvokeAsFunction(params object[] args) =>
            scriptObject.InvokeAsFunction(args);
    }
}
