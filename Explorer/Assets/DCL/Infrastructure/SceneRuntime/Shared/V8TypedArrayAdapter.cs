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

        // Reused per-instance to avoid per-call array allocation in hot paths
        private static readonly object s_boxedZero = (object)0;
        private readonly object[] _subarrayArgs = new object[2];
        private V8ArrayBufferAdapter? _cachedArrayBuffer;

        // Cached delegate + fields to eliminate per-WriteBytes-call closure allocation
        private IntPtr _writeSrcPtr;
        private ulong _writeCount;
        private ulong _writeOffset;
        private readonly Action<IntPtr> _writeBytesDelegate;

        ulong IDCLTypedArray<byte>.Length => TypedArray.Length;

        ulong IDCLTypedArray<byte>.Size => TypedArray.Size;

        IDCLArrayBuffer IDCLTypedArray<byte>.ArrayBuffer
        {
            get
            {
                if (_cachedArrayBuffer == null)
                    _cachedArrayBuffer = new V8ArrayBufferAdapter(TypedArray.ArrayBuffer);
                return _cachedArrayBuffer;
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
            _subarrayArgs[0] = s_boxedZero;
            _writeBytesDelegate = ExecuteWriteBytes;
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
            // Store call data in fields and use a pre-allocated delegate to avoid a closure allocation per call.
            unsafe
            {
                fixed (byte* srcPtr = &MemoryMarshal.GetReference(source))
                {
                    _writeSrcPtr = new IntPtr(srcPtr + sourceIndex);
                    _writeCount = count;
                    _writeOffset = offset;
                    TypedArray.InvokeWithDirectAccess(_writeBytesDelegate);
                }
            }
        }

        private unsafe void ExecuteWriteBytes(IntPtr pData)
        {
            byte* destPtr = (byte*)pData.ToPointer() + _writeOffset;
            Buffer.MemoryCopy(_writeSrcPtr.ToPointer(), destPtr, _writeCount, _writeCount);
        }

        IDCLTypedArray<byte> IDCLTypedArray<byte>.Subarray(int from, int to)
        {
            _subarrayArgs[0] = from == 0 ? s_boxedZero : (object)from;
            _subarrayArgs[1] = to;
            return new V8TypedArrayAdapter((ITypedArray<byte>)TypedArray.InvokeMethod("subarray", _subarrayArgs));
        }

        object IDCLScriptObject.GetProperty(string name, params object[] args) =>
            ScriptObject.GetProperty(name, args);

        object IDCLScriptObject.GetProperty(string name) =>
            ScriptObject.GetProperty(name);

        void IDCLScriptObject.SetProperty(string name, params object[] args) =>
            ScriptObject.SetProperty(name, args);

        void IDCLScriptObject.SetProperty(string name, object value) =>
            ScriptObject.SetProperty(name, value);

        void IDCLScriptObject.SetProperty(int index, object value)
        {
            object valueToSet = value;
            if (value is V8ScriptObjectAdapter v8Adapter)
                valueToSet = v8Adapter.ScriptObject;
            else if (value is V8TypedArrayAdapter v8TypedAdapter)
                valueToSet = v8TypedAdapter.ScriptObject;
            ScriptObject.SetProperty(index, valueToSet);
        }

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
                return new V8ScriptObjectAdapter(so);

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
