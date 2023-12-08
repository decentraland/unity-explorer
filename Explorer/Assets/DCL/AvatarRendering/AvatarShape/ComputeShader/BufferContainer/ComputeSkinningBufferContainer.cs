using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public abstract class ComputeSkinningBufferContainer : IDisposable
    {
        internal static readonly ListObjectPool<Matrix4x4> MATRIX4X4_POOL = new (listInstanceDefaultCapacity: ComputeShaderConstants.BONE_COUNT);

        //5000 is an approximation of a top value a wearable may have for its vertex count
        internal static readonly ListObjectPool<Vector3> VECTOR3_POOL = new (defaultCapacity: 2, listInstanceDefaultCapacity: 5000);
        internal static readonly ListObjectPool<Vector4> VECTOR4_POOL = new (listInstanceDefaultCapacity: 5000);
        internal static readonly ListObjectPool<BoneWeight> BONE_WEIGHT_POOL = new (listInstanceDefaultCapacity: 5000);
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

        public void Dispose()
        {
            vertexIn.Dispose();
            normalsIn.Dispose();
            tangentsIn.Dispose();
            sourceSkin.Dispose();
            bindPosesIndex.Dispose();
            bindPoses.Dispose();
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
            List<Matrix4x4> bindPosesList = MATRIX4X4_POOL.Get();
            mesh.GetBindposes(bindPosesList);
            NativeArray<Matrix4x4>.Copy(UnsafeUtility.As<List<Matrix4x4>, ListPrivateFieldAccess<Matrix4x4>>(ref bindPosesList)._items, 0, bindPosesMatrix, ComputeShaderConstants.BONE_COUNT * skinnedMeshCounter, ComputeShaderConstants.BONE_COUNT);
            MATRIX4X4_POOL.Release(bindPosesList);

            List<BoneWeight> boneWeightPool = BONE_WEIGHT_POOL.Get();
            mesh.GetBoneWeights(boneWeightPool);
            NativeArray<BoneWeight>.Copy(UnsafeUtility.As<List<BoneWeight>, ListPrivateFieldAccess<BoneWeight>>(ref boneWeightPool)._items, 0, totalSkinIn, vertexCounter, currentMeshVertexCount);
            BONE_WEIGHT_POOL.Release(boneWeightPool);

            List<Vector3> verticesPool = VECTOR3_POOL.Get();
            mesh.GetVertices(verticesPool);
            NativeArray<Vector3>.Copy(UnsafeUtility.As<List<Vector3>, ListPrivateFieldAccess<Vector3>>(ref verticesPool)._items, 0, totalVertsIn, vertexCounter, currentMeshVertexCount);
            VECTOR3_POOL.Release(verticesPool);

            List<Vector3> normalsPool = VECTOR3_POOL.Get();
            mesh.GetNormals(normalsPool);
            NativeArray<Vector3>.Copy(UnsafeUtility.As<List<Vector3>, ListPrivateFieldAccess<Vector3>>(ref normalsPool)._items, 0, totalNormalsIn, vertexCounter, currentMeshVertexCount);
            VECTOR3_POOL.Release(normalsPool);

            List<Vector4> tangentsPool = VECTOR4_POOL.Get();
            mesh.GetTangents(tangentsPool);
            NativeArray<Vector4>.Copy(UnsafeUtility.As<List<Vector4>, ListPrivateFieldAccess<Vector4>>(ref tangentsPool)._items, 0, totalTangentsIn, vertexCounter, currentMeshVertexCount);
            VECTOR4_POOL.Release(tangentsPool);

            //Setup vertex index for current wearable
            for (var i = 0; i < mesh.vertexCount; i++)
                bindPosesIndexList[vertexCounter + i] = ComputeShaderConstants.BONE_COUNT * skinnedMeshCounter;
        }

        //Helper class to access private fields of List<T>
        private class ListPrivateFieldAccess<T>
        {
#pragma warning disable CS0649
#pragma warning disable CS8618
            internal T[] _items; // Do not rename (binary serialization)
        }
    }
}
