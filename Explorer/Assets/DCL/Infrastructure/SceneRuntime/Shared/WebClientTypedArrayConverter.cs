#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
using System;
using Utility;

namespace SceneRuntime.WebClient
{
    /// <summary>
    ///     WebGL implementation of <see cref="ITypedArrayConverter" /> that enforces that only
    ///     <see cref="IDCLTypedArray{T}" /> instances (already in the correct form) are passed in.
    ///     Unlike the desktop V8 path, WebGL never receives raw <c>ITypedArray</c> objects from ClearScript,
    ///     so any non-<see cref="IDCLTypedArray{T}" /> value is treated as a programming error.
    /// </summary>
    public class WebClientTypedArrayConverter : ITypedArrayConverter
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
#endif
