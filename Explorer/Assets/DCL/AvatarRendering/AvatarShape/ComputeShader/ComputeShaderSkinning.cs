using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeShaderSkinning : CustomSkinning
    {
        private int skinnedMeshRendererBoneCount;
        private int kernel;
        private int skinnedMeshRendererCount;
        private int vertCount;

        private UnityEngine.ComputeShader cs;
        private ComputeBuffer mBones;

        private readonly List<UsedTextureArraySlot> usedTextureArraySlots;

        //TODO: Find out why adding ComputeBufferType.Constant doesnt work in Windows, but it does in Mac
        private ComputeBuffer vertexIn;
        private ComputeBuffer tangentsIn;
        private ComputeBuffer normalsIn;
        private ComputeBuffer sourceSkin;
        private ComputeBuffer bindPoses;
        private ComputeBuffer bindPosesIndex;

        public ComputeShaderSkinning()
        {
            usedTextureArraySlots = new List<UsedTextureArraySlot>();
        }

        public override void ComputeSkinning(NativeArray<float4x4> bonesResult)
        {
            mBones.SetData(bonesResult);
            cs.Dispatch(kernel, (vertCount / 64) + 1, 1, 1);
        }

        public override int Initialize(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer,
            UnityEngine.ComputeShader skinningShader, Material avatarMaterial, int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer)
        {
            SetupCounters(gameObjects);
            SetupComputeShader(gameObjects, skinningShader, lastAvatarVertCount);
            SetupMeshRenderer(gameObjects, textureArrayContainer, avatarMaterial, lastAvatarVertCount, baseAvatarSkinnedMeshRenderer);
            return vertCount;
        }

        //TODO: Pool this buffer
        private ComputeBuffer CreateSubUpdateBuffer<T>(int size) where T: struct =>
            new (size, Marshal.SizeOf(typeof(T)), ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);

        private void SetupComputeShader(List<GameObject> gameObjects, UnityEngine.ComputeShader skinningShader, int lastAvatarVertCount)
        {
            vertexIn = CreateSubUpdateBuffer<Vector3>(vertCount);
            NativeArray<Vector3> totalVertsIn = vertexIn.BeginWrite<Vector3>(0, vertCount);
            normalsIn = CreateSubUpdateBuffer<Vector3>(vertCount);
            NativeArray<Vector3> totalNormalsIn = normalsIn.BeginWrite<Vector3>(0, vertCount);
            tangentsIn = CreateSubUpdateBuffer<Vector4>(vertCount);
            NativeArray<Vector4> totalTangentsIn = tangentsIn.BeginWrite<Vector4>(0, vertCount);
            sourceSkin = CreateSubUpdateBuffer<BoneWeight>(vertCount);
            NativeArray<BoneWeight> totalSkinIn = sourceSkin.BeginWrite<BoneWeight>(0, vertCount);
            bindPosesIndex = CreateSubUpdateBuffer<int>(vertCount);
            NativeArray<int> bindPosesIndexList = bindPosesIndex.BeginWrite<int>(0, vertCount);
            bindPoses = CreateSubUpdateBuffer<Matrix4x4>(skinnedMeshRendererBoneCount);
            NativeArray<Matrix4x4> bindPosesMatrix = bindPoses.BeginWrite<Matrix4x4>(0, skinnedMeshRendererBoneCount);

            var vertCounter = 0;
            var skinnedMeshCounter = 0;

            foreach (GameObject gameObject in gameObjects)
            {
                Transform rootTransform = gameObject.transform;

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int meshVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                    ResetTransforms(skinnedMeshRenderer, rootTransform);
                    FillMeshArray(skinnedMeshRenderer, bindPosesMatrix, bindPosesIndexList, totalVertsIn, totalNormalsIn, totalTangentsIn, totalSkinIn, meshVertexCount, vertCounter, skinnedMeshCounter);
                    vertCounter += meshVertexCount;
                    skinnedMeshCounter++;
                }
            }

            SetupBuffers(skinningShader, lastAvatarVertCount);
        }

        private void SetupBuffers(UnityEngine.ComputeShader skinningShader, int lastAvatarVertCount)
        {
            //TODO: Find out why adding ComputeBufferType.Constant doesnt work in Windows, but it does in Mac
            vertexIn.EndWrite<Vector3>(vertCount);
            normalsIn.EndWrite<Vector3>(vertCount);
            tangentsIn.EndWrite<Vector4>(vertCount);
            sourceSkin.EndWrite<BoneWeight>(vertCount);
            bindPosesIndex.EndWrite<int>(vertCount);
            bindPoses.EndWrite<Matrix4x4>(skinnedMeshRendererBoneCount);
            mBones = new ComputeBuffer(ComputeShaderHelpers.BONE_COUNT, Marshal.SizeOf(typeof(Matrix4x4)));


            cs = Object.Instantiate(skinningShader);
            kernel = cs.FindKernel(ComputeShaderHelpers.SKINNING_KERNEL_NAME);
            cs.SetInt(ComputeShaderHelpers.VERT_COUNT_ID, vertCount);
            cs.SetInt(ComputeShaderHelpers.LAST_AVATAR_VERT_COUNT_ID, lastAvatarVertCount);
            cs.SetBuffer(kernel, ComputeShaderHelpers.VERTS_IN_ID, vertexIn);
            cs.SetBuffer(kernel, ComputeShaderHelpers.NORMALS_IN_ID, normalsIn);
            cs.SetBuffer(kernel, ComputeShaderHelpers.TANGENTS_IN_ID, tangentsIn);
            cs.SetBuffer(kernel, ComputeShaderHelpers.SOURCE_SKIN_ID, sourceSkin);
            cs.SetBuffer(kernel, ComputeShaderHelpers.BIND_POSE_ID, bindPoses);
            cs.SetBuffer(kernel, ComputeShaderHelpers.BIND_POSES_INDEX_ID, bindPosesIndex);
            cs.SetBuffer(kernel, ComputeShaderHelpers.BONES_ID, mBones);
        }

        private void FillMeshArray(SkinnedMeshRenderer skinnedMeshRenderer, NativeArray<Matrix4x4> bindPosesMatrix,
            NativeArray<int> bindPosesIndexList, NativeArray<Vector3> totalVertsIn, NativeArray<Vector3> totalNormalsIn, NativeArray<Vector4> totalTangentsIn, NativeArray<BoneWeight> totalSkinIn,
            int currentMeshVertexCount, int vertexCounter, int skinnedMeshCounter)
        {
            Mesh mesh = skinnedMeshRenderer.sharedMesh;

            mesh.RecalculateTangents();

            NativeArray<Matrix4x4>.Copy(mesh.bindposes, 0, bindPosesMatrix, ComputeShaderHelpers.BONE_COUNT * skinnedMeshCounter, ComputeShaderHelpers.BONE_COUNT);
            NativeArray<BoneWeight>.Copy(mesh.boneWeights, 0, totalSkinIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector3>.Copy(mesh.vertices, 0, totalVertsIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector3>.Copy(mesh.normals, 0, totalNormalsIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector4>.Copy(mesh.tangents, 0, totalTangentsIn, vertexCounter, currentMeshVertexCount);

            //Setup vertex index for current wearable
            for (var i = 0; i < mesh.vertexCount; i++)
                bindPosesIndexList[vertexCounter + i] = ComputeShaderHelpers.BONE_COUNT * skinnedMeshCounter;
        }

        private void SetupCounters(List<GameObject> gameObjects)
        {
            foreach (GameObject gameObject in gameObjects)
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                vertCount += skinnedMeshRenderer.sharedMesh.vertexCount;
                skinnedMeshRendererCount++;
            }

            skinnedMeshRendererBoneCount = skinnedMeshRendererCount * ComputeShaderHelpers.BONE_COUNT;
        }

        private void SetupMeshRenderer(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer, Material avatarMaterial, int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer)
        {
            var auxVertCounter = 0;

            foreach (GameObject gameObject in gameObjects)
            {
                foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int currentVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                    Renderer renderer = SetupMesh(skinnedMeshRenderer, baseAvatarSkinnedMeshRenderer);
                    SetupMaterial(renderer, auxVertCounter, textureArrayContainer, avatarMaterial, lastAvatarVertCount);
                    auxVertCounter += currentVertexCount;
                }
            }
        }

        private Renderer SetupMesh(SkinnedMeshRenderer skin, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer)
        {
            GameObject go = skin.gameObject;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            filter.mesh = skin.sharedMesh;
            meshRenderer.material = skin.material;
            Object.Destroy(skin);
            return meshRenderer;
        }

        protected override void SetupMaterial(Renderer meshRenderer, int lastWearableVertCount, TextureArrayContainer textureArrayContainer, Material celShadingMaterial, int lastAvatarVertCount)
        {
            var vertOutMaterial = new Material(celShadingMaterial);

            var albedoTexture = (Texture2D)meshRenderer.material.mainTexture;

            if (albedoTexture != null)
            {
                UsedTextureArraySlot usedIndex = textureArrayContainer.SetTexture(vertOutMaterial, albedoTexture, ComputeShaderHelpers.TextureArrayType.ALBEDO);
                usedTextureArraySlots.Add(usedIndex);
            }

            foreach (string keyword in ComputeShaderHelpers.keywordsToCheck)
            {
                if (meshRenderer.material.IsKeywordEnabled(keyword))
                    vertOutMaterial.EnableKeyword(keyword);
            }

            //vertOutMaterial.SetColor(ComputeShaderHelpers._BaseColour_ShaderID, Color.red);
            meshRenderer.material = vertOutMaterial;
            vertOutMaterial.SetInteger("_useCompute", 1);
            vertOutMaterial.SetInteger(ComputeShaderHelpers.LAST_AVATAR_VERT_COUNT_ID, lastWearableVertCount);
            vertOutMaterial.SetInteger(ComputeShaderHelpers.LAST_WEARABLE_VERT_COUNT_ID, lastAvatarVertCount);
        }

        public new void Dispose()
        {
            //foreach (UsedTextureArraySlot usedTextureArraySlot in usedTextureArraySlots)
            //    textureArrayContainer.FreeTexture(usedTextureArraySlot);

            usedTextureArraySlots.Clear();
        }
    }
}
