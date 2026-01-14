using SceneRuntime;
using System;
using Utility;

namespace SceneRuntime.Web
{
    public class WebGLTypedArrayConverter : ITypedArrayConverter
    {
        public IDCLTypedArray<byte> Convert(object typedArray)
        {
            if (typedArray is IDCLTypedArray<byte> dclTypedArray)
                return dclTypedArray;

            // WebGL should only receive IDCLTypedArray, not ITypedArray
            throw new ArgumentException($"WebGL expects IDCLTypedArray<byte>, but got {typedArray?.GetType()}", nameof(typedArray));
        }
    }
}
