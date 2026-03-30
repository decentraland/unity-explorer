#if !UNITY_WEBGL || (UNITY_EDITOR && !EDITOR_DEBUG_WEBGL)
using Microsoft.ClearScript.JavaScript;
using System;
using Utility;

namespace SceneRuntime.V8
{
    /// <summary>
    /// Adapts ClearScript's <see cref="Microsoft.ClearScript.JavaScript.IArrayBuffer"/> to <see cref="IDCLArrayBuffer"/>,
    /// providing read/write byte access and unsafe direct-pointer (<see cref="IntPtr"/>) access to the underlying V8 buffer.
    /// </summary>
    public class V8ArrayBufferAdapter : IDCLArrayBuffer
    {
        private readonly IArrayBuffer arrayBuffer;

        public V8ArrayBufferAdapter(IArrayBuffer arrayBuffer)
        {
            this.arrayBuffer = arrayBuffer;
        }

        public IArrayBuffer ArrayBuffer => arrayBuffer;

        ulong IDCLArrayBuffer.Size => arrayBuffer.Size;

        ulong IDCLArrayBuffer.ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex) =>
            arrayBuffer.ReadBytes(offset, count, destination, destinationIndex);

        ulong IDCLArrayBuffer.WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset) =>
            arrayBuffer.WriteBytes(source, sourceIndex, count, offset);

        void IDCLArrayBuffer.InvokeWithDirectAccess(Action<IntPtr> action) =>
            arrayBuffer.InvokeWithDirectAccess(action);

        TResult IDCLArrayBuffer.InvokeWithDirectAccess<TResult>(Func<IntPtr, TResult> func) =>
            arrayBuffer.InvokeWithDirectAccess(func);
    }
}
#endif
