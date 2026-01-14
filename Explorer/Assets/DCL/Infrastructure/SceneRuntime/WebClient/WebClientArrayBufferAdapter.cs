using System;
using Utility;

namespace SceneRuntime.WebClient
{
    public class WebClientArrayBufferAdapter : IDCLArrayBuffer
    {
        private readonly WebClientScriptObject scriptObject;

        public WebClientArrayBufferAdapter(WebClientScriptObject scriptObject)
        {
            this.scriptObject = scriptObject;
        }

        public WebClientScriptObject ScriptObject => scriptObject;

        public static implicit operator WebClientScriptObject(WebClientArrayBufferAdapter adapter) => adapter.scriptObject;

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

            object subarrayResult = scriptObject.InvokeMethod("slice", (long)offset, (long)(offset + count));
            if (subarrayResult is WebClientScriptObject subarray)
            {
                for (ulong i = 0; i < count && i < (ulong)destination.LongLength - destinationIndex; i++)
                {
                    object byteValue = subarray.GetProperty(i.ToString());
                    if (byteValue != null)
                    {
                        destination[destinationIndex + i] = Convert.ToByte(byteValue);
                    }
                }
                return count;
            }
            return 0;
        }

        ulong IDCLArrayBuffer.WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
        {
            if (count == 0)
                return 0;

            for (ulong i = 0; i < count && i < (ulong)source.LongLength - sourceIndex; i++)
            {
                scriptObject.SetProperty((offset + i).ToString(), source[sourceIndex + i]);
            }
            return count;
        }

        void IDCLArrayBuffer.InvokeWithDirectAccess(Action<IntPtr> action)
        {
            throw new NotSupportedException("WebGL does not support direct memory access");
        }

        TResult IDCLArrayBuffer.InvokeWithDirectAccess<TResult>(Func<IntPtr, TResult> func)
        {
            throw new NotSupportedException("WebGL does not support direct memory access");
        }
    }
}
