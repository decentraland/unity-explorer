using System;
using System.Runtime.InteropServices;

namespace Microsoft.ClearScript.V8.SplitProxy
{
    public readonly ref struct Uint8Array
    {
        public int Length { get; }
        private readonly V8Object.Handle ptr;
        
        internal Uint8Array(V8Object.Handle pArray)
        {
            ptr = pArray;
            
            Length = V8SplitProxyNative.Invoke(instance =>
            {
                using var arrayBuffer = V8Value.New();

                instance.V8Object_GetArrayBufferOrViewInfo(pArray, arrayBuffer.ptr, out _, out _, 
                    out ulong length);

                return (int)length;
            });
        }

        public void CopyTo(byte[] array)
        {
            int length = Length;
            
            if (length > array.Length)
                throw new IndexOutOfRangeException(
                    $"Tried to copy {length} items to a {array.Length} item array");
            
            // TODO: Don't allocate a lambda every time.
            IntPtr pAction = V8ProxyHelpers.AddRefHostObject(new Action<IntPtr>(data =>
                Marshal.Copy(data, array, 0, length)));
            
            try
            {
                var ptr = this.ptr;
                V8SplitProxyNative.Invoke(instance => instance.V8Object_InvokeWithArrayBufferOrViewData(ptr, pAction));
            }
            finally
            {
                V8ProxyHelpers.ReleaseHostObject(pAction);
            }
        }
    }
}
