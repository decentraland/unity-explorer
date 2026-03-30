#if !UNITY_WEBGL || (UNITY_EDITOR && !EDITOR_DEBUG_WEBGL)
using Microsoft.ClearScript.JavaScript;
using System;
using Utility;

namespace SceneRuntime.V8
{
    /// <summary>
    /// Implements <see cref="ITypedArrayConverter"/> to normalise typed-array values returned from JavaScript:
    /// passes through existing <see cref="IDCLTypedArray{T}"/> instances unchanged and wraps raw ClearScript
    /// <see cref="Microsoft.ClearScript.JavaScript.ITypedArray{T}"/> values in a <see cref="V8TypedArrayAdapter"/>.
    /// </summary>
    public class V8TypedArrayConverter : ITypedArrayConverter
    {
        public IDCLTypedArray<byte> Convert(object typedArray)
        {
            if (typedArray is IDCLTypedArray<byte> dclTypedArray)
                return dclTypedArray;

            if (typedArray is ITypedArray<byte> v8TypedArray)
                return new V8TypedArrayAdapter(v8TypedArray);

            throw new ArgumentException($"Expected ITypedArray<byte> or IDCLTypedArray<byte>, but got {typedArray.GetType()}", nameof(typedArray));
        }
    }
}
#endif
