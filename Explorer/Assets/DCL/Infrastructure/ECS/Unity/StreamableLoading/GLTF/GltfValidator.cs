using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ECS.StreamableLoading.GLTF
{
    public static class GltfValidator
    {
        // The first 4 bytes of a GLB file have this unit32 magic number to identify data as Binary glTF
        // Source: https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#binary-header
        private const uint GLB_SIGNATURE = 0x46546c67;

        public static unsafe bool IsGltfBinaryFormat(NativeArray<byte>.ReadOnly data)
        {
            if (data.Length < 4) return false;
            uint gltfBinarySignature = UnsafeUtility.ReadArrayElement<uint>(data.GetUnsafeReadOnlyPtr(), 0);
            return gltfBinarySignature == GLB_SIGNATURE;
        }

        public static bool IsGltfBinaryFormat(byte[] data)
        {
            if (data.Length < 4) return false;
            uint gltfBinarySignature = BitConverter.ToUInt32(data, 0);
            return gltfBinarySignature == GLB_SIGNATURE;
        }
    }
}
