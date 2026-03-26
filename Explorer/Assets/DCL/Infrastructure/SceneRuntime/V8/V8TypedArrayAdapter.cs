#if !UNITY_WEBGL || (UNITY_EDITOR && !EDITOR_DEBUG_WEBGL)
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Utility;

namespace SceneRuntime.V8
{
    /// <summary>
    /// Dual-interface adapter that wraps ClearScript's <see cref="Microsoft.ClearScript.JavaScript.ITypedArray{T}"/> (byte)
    /// as both <see cref="IDCLTypedArray{T}"/> and <see cref="IDCLScriptObject"/>, enabling byte-level read/write,
    /// unsafe direct access, and general JavaScript object manipulation on the same array instance.
    /// </summary>
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

        void IDCLTypedArray<byte>.WriteBytes(ReadOnlySpan<byte> source, ulong sourceIndex, ulong count, ulong offset)
        {
            if (count == 0)
                return;

            // ReadOnlySpan is a ref struct and cannot be captured in a lambda.
            // Pin the data here and pass it as IntPtr so the lambda can use it safely.
            unsafe
            {
                fixed (byte* srcPtr = &MemoryMarshal.GetReference(source))
                {
                    IntPtr srcIntPtr = new IntPtr(srcPtr + sourceIndex);

                    TypedArray.InvokeWithDirectAccess(pData =>
                    {
                        unsafe
                        {
                            byte* destPtr = (byte*)pData.ToPointer() + offset;
                            Buffer.MemoryCopy(srcIntPtr.ToPointer(), destPtr, count, count);
                        }
                    });
                }
            }
        }

        object IDCLScriptObject.GetProperty(string name, params object[] args) =>
            ScriptObject.GetProperty(name, args);

        void IDCLScriptObject.SetProperty(string name, params object[] args) =>
            ScriptObject.SetProperty(name, args);

        void IDCLScriptObject.SetProperty(int index, object value) =>
            ScriptObject.SetProperty(index, value);

        object IDCLScriptObject.Invoke(bool asConstructor, params object[] args)
        {
            object result = ScriptObject.Invoke(asConstructor, args);

            if (result is ITypedArray<byte> ta)
                return new V8TypedArrayAdapter(ta);

            if (result is ScriptObject so)
                return new V8ScriptObjectAdapter(so);

            return result;
        }

        object IDCLScriptObject.InvokeMethod(string name, params object[] args)
        {
            object result = ScriptObject.InvokeMethod(name, args);

            if (result is ITypedArray<byte> ta)
                return new V8TypedArrayAdapter(ta);

            if (result is ScriptObject so)
            {
                if (so is ITypedArray<byte> soAsTypedArray)
                    return new V8TypedArrayAdapter(soAsTypedArray);

                return new V8ScriptObjectAdapter(so);
            }

            return result;
        }

        object IDCLScriptObject.InvokeAsFunction(params object[] args) =>
            ScriptObject.InvokeAsFunction(args);

        /// <inheritdoc />
        object IDCLScriptObject.GetNativeObject() =>
            ScriptObject;
    }
}
#endif
