using System;
using Unity.Collections;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public abstract class ComputeSkinningBufferContainer : IDisposable
    {
        protected ComputeBuffer vertexIn;
        protected ComputeBuffer tangentsIn;
        protected ComputeBuffer normalsIn;
        protected ComputeBuffer sourceSkin;
        protected ComputeBuffer bindPoses;
        protected ComputeBuffer bindPosesIndex;

        protected NativeArray<Vector3> totalVertsIn;
        protected NativeArray<Vector3> totalNormalsIn;
        protected NativeArray<Vector4> totalTangentsIn;
        protected NativeArray<BoneWeight> totalSkinIn;
        protected NativeArray<int> bindPosesIndexList;
        protected NativeArray<Matrix4x4> bindPosesMatrix;

        protected int vertCount;
        protected int skinnedMeshRendererBoneCount;

        public ComputeSkinningBufferContainer(int vertCount, int skinnedMeshRenderersConeCount)
        {
            this.vertCount = vertCount;
            skinnedMeshRendererBoneCount = skinnedMeshRenderersConeCount;
        }

        public abstract void StartWriting();

        public abstract void EndWriting();

        public void SetBuffers(UnityEngine.ComputeShader cs, int kernel)
        {
            cs.SetBuffer(kernel, ComputeShaderConstants.VERTS_IN_ID, vertexIn);
            cs.SetBuffer(kernel, ComputeShaderConstants.NORMALS_IN_ID, normalsIn);
            cs.SetBuffer(kernel, ComputeShaderConstants.TANGENTS_IN_ID, tangentsIn);
            cs.SetBuffer(kernel, ComputeShaderConstants.SOURCE_SKIN_ID, sourceSkin);
            cs.SetBuffer(kernel, ComputeShaderConstants.BIND_POSE_ID, bindPoses);
            cs.SetBuffer(kernel, ComputeShaderConstants.BIND_POSES_INDEX_ID, bindPosesIndex);
        }

        public void CopyAllBuffers(Mesh mesh, int currentMeshVertexCount, int vertexCounter, int skinnedMeshCounter)
        {
            NativeArray<Matrix4x4>.Copy(mesh.bindposes, 0, bindPosesMatrix, ComputeShaderConstants.BONE_COUNT * skinnedMeshCounter, ComputeShaderConstants.BONE_COUNT);
            NativeArray<BoneWeight>.Copy(mesh.boneWeights, 0, totalSkinIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector3>.Copy(mesh.vertices, 0, totalVertsIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector3>.Copy(mesh.normals, 0, totalNormalsIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector4>.Copy(mesh.tangents, 0, totalTangentsIn, vertexCounter, currentMeshVertexCount);

            //Setup vertex index for current wearable
            for (var i = 0; i < mesh.vertexCount; i++)
                bindPosesIndexList[vertexCounter + i] = ComputeShaderConstants.BONE_COUNT * skinnedMeshCounter;
        }

        public void Dispose()
        {
            vertexIn.Dispose();
            normalsIn.Dispose();
            tangentsIn.Dispose();
            sourceSkin.Dispose();
            bindPosesIndex.Dispose();
            bindPoses.Dispose();
        }
    }
}
