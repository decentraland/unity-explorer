using System;
using System.Collections.Generic;
using Utility;

namespace SceneRuntime.Web
{
    public class WebGLTypedArrayAdapter : IDCLTypedArray<byte>, IDCLScriptObject
    {
        private readonly WebGLScriptObject scriptObject;

        public WebGLTypedArrayAdapter(WebGLScriptObject scriptObject)
        {
            this.scriptObject = scriptObject;
        }

        public WebGLScriptObject ScriptObject => scriptObject;

        public static implicit operator WebGLScriptObject(WebGLTypedArrayAdapter adapter) => adapter.scriptObject;

        public ulong Length
        {
            get
            {
                object length = scriptObject.GetProperty("length");
                return length != null ? Convert.ToUInt64(length) : 0;
            }
        }

        ulong IDCLTypedArray<byte>.Size
        {
            get
            {
                object length = scriptObject.GetProperty("length");
                return length != null ? Convert.ToUInt64(length) : 0;
            }
        }

        IDCLArrayBuffer IDCLTypedArray<byte>.ArrayBuffer
        {
            get
            {
                object buffer = scriptObject.GetProperty("buffer");
                if (buffer is WebGLScriptObject bufferObj)
                    return new WebGLArrayBufferAdapter(bufferObj);
                throw new InvalidOperationException("Failed to get buffer property from typed array");
            }
        }

        ulong IDCLTypedArray<byte>.Read(ulong index, ulong length, byte[] destination, ulong destinationIndex)
        {
            if (length == 0)
                return 0;

            ulong actualLength = Math.Min(length, (ulong)destination.LongLength - destinationIndex);
            actualLength = Math.Min(actualLength, this.Length - index);

            for (ulong i = 0; i < actualLength; i++)
            {
                object byteValue = scriptObject.GetProperty((index + i).ToString());
                if (byteValue != null)
                {
                    destination[destinationIndex + i] = Convert.ToByte(byteValue);
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
            actualCount = Math.Min(actualCount, this.Length - offset);

            for (ulong i = 0; i < actualCount; i++)
            {
                object byteValue = scriptObject.GetProperty((offset + i).ToString());
                if (byteValue != null)
                {
                    destination[destinationIndex + i] = Convert.ToByte(byteValue);
                }
            }
        }

        void IDCLTypedArray<byte>.WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
        {
            if (count == 0)
                return;

            ulong actualCount = Math.Min(count, (ulong)source.LongLength - sourceIndex);
            actualCount = Math.Min(actualCount, this.Length - offset);

            for (ulong i = 0; i < actualCount; i++)
            {
                scriptObject.SetProperty((offset + i).ToString(), source[sourceIndex + i]);
            }
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

        object IDCLScriptObject.Invoke(bool asConstructor, params object[] args)
        {
            object result = scriptObject.Invoke(asConstructor, args);
            if (result is WebGLScriptObject wso)
                return new WebGLTypedArrayAdapter(wso);
            return result;
        }

        object IDCLScriptObject.InvokeMethod(string name, params object[] args)
        {
            object result = scriptObject.InvokeMethod(name, args);
            if (result is WebGLScriptObject wso)
                return new WebGLTypedArrayAdapter(wso);
            return result;
        }

        object IDCLScriptObject.InvokeAsFunction(params object[] args) =>
            scriptObject.InvokeAsFunction(args);
    }
}
