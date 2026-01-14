using Microsoft.ClearScript.JavaScript;
using System;
using Utility;

namespace SceneRuntime.V8
{
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
