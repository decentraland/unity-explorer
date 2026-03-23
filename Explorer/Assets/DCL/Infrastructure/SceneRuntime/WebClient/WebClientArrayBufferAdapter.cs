using System;
using System.Runtime.InteropServices;
using Utility;

namespace SceneRuntime.WebClient
{
    /// <summary>
    ///     Adapts a JavaScript <c>ArrayBuffer</c> (held as a <see cref="WebClientScriptObject" />) to the
    ///     <see cref="IDCLArrayBuffer" /> interface. Byte reads and writes use the jslib's
    ///     <c>JSContext_ReadObjectBytesIntoBuffer</c> / <c>JSContext_WriteObjectBytesFromBuffer</c> calls to avoid
    ///     copying data through JSON. Direct pointer access is not supported on WebGL.
    /// </summary>
    public class WebClientArrayBufferAdapter : IDCLArrayBuffer
    {
        private readonly WebClientScriptObject scriptObject;

        public WebClientArrayBufferAdapter(WebClientScriptObject scriptObject)
        {
            this.scriptObject = scriptObject;
        }

        public WebClientScriptObject ScriptObject => scriptObject;

        public static implicit operator WebClientScriptObject(WebClientArrayBufferAdapter adapter) =>
            adapter.scriptObject;

        ulong IDCLArrayBuffer.Size
        {
            get
            {
                object byteLength = scriptObject.GetProperty("byteLength");
                return byteLength != null ? Convert.ToUInt64(byteLength) : 0;
            }
        }

        ulong IDCLArrayBuffer.ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex)
        {
            if (count == 0)
                return 0;

            ulong actualCount = Math.Min(count, (ulong)destination.LongLength - destinationIndex);

            if (actualCount > int.MaxValue)
                throw new OverflowException($"[WebClientArrayBufferAdapter] Operation length {actualCount} exceeds int.MaxValue.");

            unsafe
            {
                fixed (byte* dstPtr = destination)
                {
                    IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(scriptObject.ContextId);
                    IntPtr objectIdPtr = Utf8Marshal.StringToHGlobalUTF8(scriptObject.ObjectId);

                    try { JSContext_ReadObjectBytesIntoBuffer(contextIdPtr, objectIdPtr, (int)offset, (int)actualCount, (IntPtr)(dstPtr + (int)destinationIndex)); }
                    finally
                    {
                        Marshal.FreeHGlobal(contextIdPtr);
                        Marshal.FreeHGlobal(objectIdPtr);
                    }
                }
            }

            return actualCount;
        }

        ulong IDCLArrayBuffer.WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
        {
            if (count == 0)
                return 0;

            ulong actualCount = Math.Min(count, (ulong)source.LongLength - sourceIndex);

            if (actualCount > int.MaxValue)
                throw new OverflowException($"[WebClientArrayBufferAdapter] Operation length {actualCount} exceeds int.MaxValue.");

            unsafe
            {
                fixed (byte* srcPtr = source)
                {
                    IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(scriptObject.ContextId);
                    IntPtr objectIdPtr = Utf8Marshal.StringToHGlobalUTF8(scriptObject.ObjectId);

                    try { JSContext_WriteObjectBytesFromBuffer(contextIdPtr, objectIdPtr, (IntPtr)(srcPtr + (int)sourceIndex), (int)actualCount, (int)offset); }
                    finally
                    {
                        Marshal.FreeHGlobal(contextIdPtr);
                        Marshal.FreeHGlobal(objectIdPtr);
                    }
                }
            }

            return actualCount;
        }

        void IDCLArrayBuffer.InvokeWithDirectAccess(Action<IntPtr> action) =>
            throw new NotSupportedException("WebGL does not support direct memory access");

        TResult IDCLArrayBuffer.InvokeWithDirectAccess<TResult>(Func<IntPtr, TResult> func) =>
            throw new NotSupportedException("WebGL does not support direct memory access");

        [DllImport("__Internal")]
        private static extern void JSContext_ReadObjectBytesIntoBuffer(IntPtr contextId, IntPtr objectId, int srcOffset, int count, IntPtr dstPtr);

        [DllImport("__Internal")]
        private static extern int JSContext_WriteObjectBytesFromBuffer(IntPtr contextId, IntPtr objectId, IntPtr srcPtr, int count, int dstOffset);
    }
}
