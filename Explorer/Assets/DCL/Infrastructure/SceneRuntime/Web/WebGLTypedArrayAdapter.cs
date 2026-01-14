using System;
using System.Collections.Generic;
using Utility;

namespace SceneRuntime.Web
{
    public class WebGLTypedArrayAdapter : IDCLTypedArray<byte>, IDCLScriptObject
    {
        public WebGLScriptObject ScriptObject { get; }

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

                if (buffer is WebGLScriptObject bufferObj)
                    return new WebGLArrayBufferAdapter(bufferObj);

                throw new InvalidOperationException("Failed to get buffer property from typed array");
            }
        }

        IEnumerable<string> IDCLScriptObject.PropertyNames => ScriptObject.PropertyNames;

        object IDCLScriptObject.this[string name, params object[] args]
        {
            get => ScriptObject[name, args];
            set => ScriptObject[name, args] = value;
        }

        public WebGLTypedArrayAdapter(WebGLScriptObject scriptObject)
        {
            ScriptObject = scriptObject;
        }

        public static implicit operator WebGLScriptObject(WebGLTypedArrayAdapter adapter) =>
            adapter.ScriptObject;

        ulong IDCLTypedArray<byte>.Read(ulong index, ulong length, byte[] destination, ulong destinationIndex)
        {
            if (length == 0)
                return 0;

            ulong actualLength = Math.Min(length, (ulong)destination.LongLength - destinationIndex);
            actualLength = Math.Min(actualLength, Length - index);

            for (ulong i = 0; i < actualLength; i++)
            {
                object byteValue = ScriptObject.GetProperty((index + i).ToString());

                if (byteValue != null) { destination[destinationIndex + i] = Convert.ToByte(byteValue); }
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

            for (ulong i = 0; i < actualCount; i++)
            {
                object byteValue = ScriptObject.GetProperty((offset + i).ToString());

                if (byteValue != null) { destination[destinationIndex + i] = Convert.ToByte(byteValue); }
            }
        }

        void IDCLTypedArray<byte>.WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
        {
            if (count == 0)
                return;

            ulong actualCount = Math.Min(count, (ulong)source.LongLength - sourceIndex);
            actualCount = Math.Min(actualCount, Length - offset);

            for (ulong i = 0; i < actualCount; i++) { ScriptObject.SetProperty((offset + i).ToString(), source[sourceIndex + i]); }
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

            if (result is WebGLScriptObject wso)
                return new WebGLTypedArrayAdapter(wso);

            return result;
        }

        object IDCLScriptObject.InvokeMethod(string name, params object[] args)
        {
            object result = ScriptObject.InvokeMethod(name, args);

            if (result is WebGLScriptObject wso)
                return new WebGLTypedArrayAdapter(wso);

            return result;
        }

        object IDCLScriptObject.InvokeAsFunction(params object[] args) =>
            ScriptObject.InvokeAsFunction(args);

        /// <inheritdoc />
        object IDCLScriptObject.GetNativeObject() =>
            ScriptObject;
    }
}
