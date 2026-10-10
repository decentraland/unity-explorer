using Unity.Collections.LowLevel.Unsafe;

namespace Utility
{
    /// <summary>
    ///     Wrapper over void* to pass structure by pointer in a short-living context,
    ///     that otherwise is not allowed by C# Compiler
    /// </summary>
    public readonly unsafe struct ManagedTypePointer<T> where T: struct
    {
        private readonly void* address;

        public ref T Value => ref UnsafeUtility.AsRef<T>(address);

        public ManagedTypePointer(ref T value)
        {
            address = UnsafeUtility.AddressOf(ref value);
        }
    }
}
