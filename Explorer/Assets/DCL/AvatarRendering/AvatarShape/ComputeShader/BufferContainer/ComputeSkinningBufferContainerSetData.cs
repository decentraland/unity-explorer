using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeSkinningBufferContainerSetData : ComputeSkinningBufferContainer
    {
        public ComputeSkinningBufferContainerSetData(int vertCount, int skinnedMeshRenderersConeCount) : base(vertCount, skinnedMeshRenderersConeCount) { }

        //TODO ComputeShaderOptimization. Do Slicing correctly. Where should we create the preallocated array?
        /*
        private unsafe NativeArray<T> SliceNativeArray<T>(int start, int size) where T: struct
        {
            int marshalSize = Marshal.SizeOf<T>();
            NativeArray<T> newArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(preallocatedArray.Slice(start * marshalSize, (start + size) * marshalSize).GetUnsafePtr(), size, Allocator.Temp);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref newArray, AtomicSafetyHandle.Create());
            return newArray;
        }
        */

        public override void StartWriting()
        {
            totalVertsIn = new NativeArray<Vector3>(vertCount, Allocator.Temp);
            totalNormalsIn = new NativeArray<Vector3>(vertCount, Allocator.Temp);
            totalTangentsIn = new NativeArray<Vector4>(vertCount, Allocator.Temp);
            totalSkinIn = new NativeArray<BoneWeight>(vertCount, Allocator.Temp);
            bindPosesIndexList = new NativeArray<int>(vertCount, Allocator.Temp);
            bindPosesMatrix = new NativeArray<Matrix4x4>(skinnedMeshRendererBoneCount, Allocator.Temp);
        }

        // Constant buffer works on Mac but not on Windows. WebGPU expects Storage (Structured), not Uniform (Constant).
        static ComputeBufferType InputBufferType()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return ComputeBufferType.Structured;
#else
            return ComputeBufferType.Constant;
#endif
        }

        public override void EndWriting()
        {
            ComputeBufferType bufferType = InputBufferType();
            int vector3Stride = Marshal.SizeOf(typeof(Vector3));
            int vector4Stride = Marshal.SizeOf(typeof(Vector4));
            int boneWeightStride = Marshal.SizeOf(typeof(BoneWeight));
            int matrixStride = Marshal.SizeOf(typeof(Matrix4x4));
            int intStride = Marshal.SizeOf(typeof(int));

            vertexIn = new ComputeBuffer(vertCount, vector3Stride, bufferType);
            vertexIn.SetData(totalVertsIn);
            tangentsIn = new ComputeBuffer(vertCount, vector4Stride, bufferType);
            tangentsIn.SetData(totalTangentsIn);
            normalsIn = new ComputeBuffer(vertCount, vector3Stride, bufferType);
            normalsIn.SetData(totalNormalsIn);
            sourceSkin = new ComputeBuffer(vertCount, boneWeightStride, bufferType);
            sourceSkin.SetData(totalSkinIn);
            bindPoses = new ComputeBuffer(skinnedMeshRendererBoneCount, matrixStride, bufferType);
            bindPoses.SetData(bindPosesMatrix);
            bindPosesIndex = new ComputeBuffer(vertCount, intStride, bufferType);
            bindPosesIndex.SetData(bindPosesIndexList);
        }
    }
}
