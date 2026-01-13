using Utility;

namespace SceneRuntime
{
    /// <summary>
    /// Converts platform-specific typed arrays to the platform-agnostic IDCLTypedArray interface.
    /// </summary>
    public interface ITypedArrayConverter
    {
        /// <summary>
        /// Converts an object (ITypedArray or IDCLTypedArray) to IDCLTypedArray.
        /// For V8: Converts ITypedArray to IDCLTypedArray by wrapping in V8TypedArrayAdapter.
        /// For WebGL: Returns IDCLTypedArray as-is (identity conversion).
        /// </summary>
        IDCLTypedArray<byte> Convert(object typedArray);
    }
}
