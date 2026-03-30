#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Utility;

namespace SceneRuntime.WebClient
{
    /// <summary>
    ///     Adapts a JavaScript <c>Uint8Array</c> (held as a <see cref="WebClientScriptObject" />) to the
    ///     <see cref="IDCLTypedArray{T}" /> interface expected by the scene runtime.
    ///     Byte reads and writes are forwarded to the jslib via <c>JSContext_ReadObjectBytesIntoBuffer</c> /
    ///     <c>JSContext_WriteObjectBytesFromBuffer</c> rather than copying through JSON. Direct pointer access is not
    ///     supported on WebGL and will throw <see cref="NotSupportedException" />.
    /// </summary>
    public class WebClientTypedArrayAdapter : IDCLTypedArray<byte>, IDCLScriptObject
    {
        public WebClientScriptObject ScriptObject { get; }

        public ulong Length
        {
            get
            {
                object length = ScriptObject.GetProperty("length");
                return length != null ? Convert.ToUInt64(length) : 0;
            }
        }

        ulong IDCLTypedArray<byte>.Size
        {
            get
            {
                object length = ScriptObject.GetProperty("length");
                return length != null ? Convert.ToUInt64(length) : 0;
            }
        }

        IDCLArrayBuffer IDCLTypedArray<byte>.ArrayBuffer
        {
            get
            {
                object buffer = ScriptObject.GetProperty("buffer");

                if (buffer is WebClientScriptObject bufferObj)
                    return new WebClientArrayBufferAdapter(bufferObj);

                throw new InvalidOperationException("Failed to get buffer property from typed array");
            }
        }

        IEnumerable<string> IDCLScriptObject.PropertyNames => ScriptObject.PropertyNames;

        object IDCLScriptObject.this[string name, params object[] args]
        {
            get => ScriptObject[name, args];
            set => ScriptObject[name, args] = value;
        }

        public WebClientTypedArrayAdapter(WebClientScriptObject scriptObject)
        {
            ScriptObject = scriptObject;
        }

        public static implicit operator WebClientScriptObject(WebClientTypedArrayAdapter adapter) =>
            adapter.ScriptObject;

        ulong IDCLTypedArray<byte>.Read(ulong index, ulong length, byte[] destination, ulong destinationIndex)
        {
            if (length == 0)
                return 0;

            ulong actualLength = Math.Min(length, (ulong)destination.LongLength - destinationIndex);
            actualLength = Math.Min(actualLength, Length - index);

            if (actualLength > int.MaxValue)
                throw new OverflowException($"[WebClientTypedArrayAdapter] Operation length {actualLength} exceeds int.MaxValue.");

            unsafe
            {
                fixed (byte* dstPtr = destination)
                {
                    IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(ScriptObject.ContextId);
                    IntPtr objectIdPtr = Utf8Marshal.StringToHGlobalUTF8(ScriptObject.ObjectId);

                    try { JSContext_ReadObjectBytesIntoBuffer(contextIdPtr, objectIdPtr, (int)index, (int)actualLength, (IntPtr)(dstPtr + (int)destinationIndex)); }
                    finally
                    {
                        Marshal.FreeHGlobal(contextIdPtr);
                        Marshal.FreeHGlobal(objectIdPtr);
                    }
                }
            }

            return actualLength;
        }

        void IDCLTypedArray<byte>.InvokeWithDirectAccess(Action<IntPtr> action) =>
            throw new NotSupportedException("WebGL does not support direct memory access");

        int IDCLTypedArray<byte>.InvokeWithDirectAccess(Func<IntPtr, int> func) =>
            throw new NotSupportedException("WebGL does not support direct memory access");

        void IDCLTypedArray<byte>.ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex)
        {
            if (count == 0)
                return;

            ulong actualCount = Math.Min(count, (ulong)destination.LongLength - destinationIndex);
            actualCount = Math.Min(actualCount, Length - offset);

            if (actualCount > int.MaxValue)
                throw new OverflowException($"[WebClientTypedArrayAdapter] Operation length {actualCount} exceeds int.MaxValue.");

            unsafe
            {
                fixed (byte* dstPtr = destination)
                {
                    IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(ScriptObject.ContextId);
                    IntPtr objectIdPtr = Utf8Marshal.StringToHGlobalUTF8(ScriptObject.ObjectId);

                    try { JSContext_ReadObjectBytesIntoBuffer(contextIdPtr, objectIdPtr, (int)offset, (int)actualCount, (IntPtr)(dstPtr + (int)destinationIndex)); }
                    finally
                    {
                        Marshal.FreeHGlobal(contextIdPtr);
                        Marshal.FreeHGlobal(objectIdPtr);
                    }
                }
            }
        }

        void IDCLTypedArray<byte>.WriteBytes(ReadOnlySpan<byte> source, ulong sourceIndex, ulong count, ulong offset)
        {
            if (count == 0)
                return;

            ulong actualCount = Math.Min(count, (ulong)source.Length - sourceIndex);
            actualCount = Math.Min(actualCount, Length - offset);

            if (actualCount > int.MaxValue)
                throw new OverflowException($"[WebClientTypedArrayAdapter] Operation length {actualCount} exceeds int.MaxValue.");

            unsafe
            {
                fixed (byte* srcPtr = &MemoryMarshal.GetReference(source))
                {
                    IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(ScriptObject.ContextId);
                    IntPtr objectIdPtr = Utf8Marshal.StringToHGlobalUTF8(ScriptObject.ObjectId);

                    try { JSContext_WriteObjectBytesFromBuffer(contextIdPtr, objectIdPtr, (IntPtr)(srcPtr + (int)sourceIndex), (int)actualCount, (int)offset); }
                    finally
                    {
                        Marshal.FreeHGlobal(contextIdPtr);
                        Marshal.FreeHGlobal(objectIdPtr);
                    }
                }
            }
        }

        [DllImport("__Internal")]
        private static extern int JSContext_WriteObjectBytesFromBuffer(IntPtr contextId, IntPtr objectId, IntPtr srcPtr, int count, int dstOffset);

        [DllImport("__Internal")]
        private static extern void JSContext_ReadObjectBytesIntoBuffer(IntPtr contextId, IntPtr objectId, int srcOffset, int count, IntPtr dstPtr);

        // Reused per-instance to avoid per-call array allocation in hot paths
        private readonly object[] _subarrayArgs = new object[2];

        IDCLTypedArray<byte> IDCLTypedArray<byte>.Subarray(int from, int to)
        {
            _subarrayArgs[0] = from;
            _subarrayArgs[1] = to;
            object result = ScriptObject.InvokeMethod("subarray", _subarrayArgs);

            if (result is WebClientScriptObject wso)
                return new WebClientTypedArrayAdapter(wso);

            throw new InvalidOperationException("subarray did not return a WebClientScriptObject");
        }

        object IDCLScriptObject.GetProperty(string name, params object[] args) =>
            ScriptObject.GetProperty(name, args);

        object IDCLScriptObject.GetProperty(string name) =>
            ScriptObject.GetProperty(name);

        void IDCLScriptObject.SetProperty(string name, params object[] args) =>
            ScriptObject.SetProperty(name, args);

        void IDCLScriptObject.SetProperty(string name, object value) =>
            ScriptObject.SetProperty(name, value);

        void IDCLScriptObject.SetProperty(int index, object value) =>
            ScriptObject.SetProperty(index, value);

        object IDCLScriptObject.Invoke(bool asConstructor, params object[] args)
        {
            object result = ScriptObject.Invoke(asConstructor, args);

            if (result is WebClientScriptObject wso)
                return new WebClientTypedArrayAdapter(wso);

            return result;
        }

        object IDCLScriptObject.InvokeMethod(string name, params object[] args)
        {
            object result = ScriptObject.InvokeMethod(name, args);

            if (result is WebClientScriptObject wso)
                return new WebClientTypedArrayAdapter(wso);

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
