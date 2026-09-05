using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeSkinningBufferContainerWrite : ComputeSkinningBufferContainer
    {
        public ComputeSkinningBufferContainerWrite(int vertCount, int skinnedMeshRenderersConeCount) : base(vertCount, skinnedMeshRenderersConeCount) { }

        //TODO ComputeShaderOptimization.  Pool this buffer
        private ComputeBuffer CreateSubUpdateBuffer<T>(int size) where T: struct =>
            new (size, Unsafe.SizeOf<T>(), ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);

        public override void StartWriting()
        {
            vertexIn = CreateSubUpdateBuffer<Vector3>(vertCount);
            totalVertsIn = vertexIn.BeginWrite<Vector3>(0, vertCount);
            normalsIn = CreateSubUpdateBuffer<Vector3>(vertCount);
            totalNormalsIn = normalsIn.BeginWrite<Vector3>(0, vertCount);
            tangentsIn = CreateSubUpdateBuffer<Vector4>(vertCount);
            totalTangentsIn = tangentsIn.BeginWrite<Vector4>(0, vertCount);
            sourceSkin = CreateSubUpdateBuffer<BoneWeight>(vertCount);
            totalSkinIn = sourceSkin.BeginWrite<BoneWeight>(0, vertCount);
            bindPosesIndex = CreateSubUpdateBuffer<int>(vertCount);
            bindPosesIndexList = bindPosesIndex.BeginWrite<int>(0, vertCount);
            bindPoses = CreateSubUpdateBuffer<Matrix4x4>(skinnedMeshRendererBoneCount);
            bindPosesMatrix = bindPoses.BeginWrite<Matrix4x4>(0, skinnedMeshRendererBoneCount);
        }

        public override void EndWriting()
        {
            vertexIn.EndWrite<Vector3>(vertCount);
            normalsIn.EndWrite<Vector3>(vertCount);
            tangentsIn.EndWrite<Vector4>(vertCount);
            sourceSkin.EndWrite<BoneWeight>(vertCount);
            bindPosesIndex.EndWrite<int>(vertCount);
            bindPoses.EndWrite<Matrix4x4>(skinnedMeshRendererBoneCount);
        }
    }
}
