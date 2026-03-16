using Microsoft.ClearScript.JavaScript;
using System;
using Utility;

namespace SceneRuntime.V8
{
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
