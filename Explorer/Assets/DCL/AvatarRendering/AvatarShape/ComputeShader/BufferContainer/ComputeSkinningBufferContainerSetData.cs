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

        //Constant buffer works in MAC but now in WINDOWS
        public override void EndWriting()
        {
            vertexIn = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Constant);
            vertexIn.SetData(totalVertsIn);
            tangentsIn = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Constant);
            tangentsIn.SetData(totalTangentsIn);
            normalsIn = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Constant);
            normalsIn.SetData(totalNormalsIn);
            sourceSkin = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(BoneWeight)), ComputeBufferType.Constant);
            sourceSkin.SetData(totalSkinIn);
            bindPoses = new ComputeBuffer(skinnedMeshRendererBoneCount, Marshal.SizeOf(typeof(Matrix4x4)), ComputeBufferType.Constant);
            bindPoses.SetData(bindPosesMatrix);
            bindPosesIndex = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Constant);
            bindPosesIndex.SetData(bindPosesIndexList);
        }
    }
}
