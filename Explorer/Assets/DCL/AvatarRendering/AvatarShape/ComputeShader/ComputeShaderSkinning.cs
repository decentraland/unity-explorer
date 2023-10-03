using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeShaderSkinning : CustomSkinning
    {
        private int vertCount;
        private int skinnedMeshRendererBoneCount;
        private int kernel;

        private UnityEngine.ComputeShader cs;
        private ComputeBuffer mBones;
        private ComputeSkinningBufferContainer computeSkinningBufferContainer;

        private readonly List<UsedTextureArraySlot> usedTextureArraySlots;


        public ComputeShaderSkinning()
        {
            usedTextureArraySlots = new List<UsedTextureArraySlot>();
        }

        public override void ComputeSkinning(NativeArray<float4x4> bonesResult)
        {
            mBones.SetData(bonesResult);
            cs.Dispatch(kernel, (vertCount / 64) + 1, 1, 1);

            //Note (Juani): According to Unity, BeginWrite/EndWrite works better than SetData. But we got inconsitent result using ComputeBufferMode.SubUpdates
            //Ash machine (AMD) worked way worse than mine (NVidia). So, we are back to SetData with a ComputeBufferMode.Dynamic, which works well for both.
            //https://docs.unity3d.com/2020.1/Documentation/ScriptReference/ComputeBuffer.BeginWrite.html
            /*NativeArray<float4x4> bonesIn = mBones.BeginWrite<float4x4>(0, ComputeShaderConstants.BONE_COUNT);
            NativeArray<float4x4>.Copy(bonesResult, 0, bonesIn, 0, ComputeShaderConstants.BONE_COUNT);
            mBones.EndWrite<float4x4>(ComputeShaderConstants.BONE_COUNT);*/
        }

        public override int Initialize(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer,
            UnityEngine.ComputeShader skinningShader, IObjectPool<Material> avatarMaterialPool, int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer, AvatarShapeComponent avatarShapeComponent)
        {
            SetupCounters(gameObjects);
            SetupComputeShader(gameObjects, skinningShader, lastAvatarVertCount);
            SetupMeshRenderer(gameObjects, textureArrayContainer, avatarMaterialPool, lastAvatarVertCount, avatarShapeComponent);
            return vertCount;
        }

        private void SetupComputeShader(List<GameObject> gameObjects, UnityEngine.ComputeShader skinningShader, int lastAvatarVertCount)
        {
            //Note (Juani): Using too many BeginWrite in Mac caused a crash. So I ve set up this switch that changes the way in which we
            //set up the buffers depending on the platform
#if UNITY_STANDALONE_WIN
            computeSkinningBufferContainer = new ComputeSkinningBufferContainerWrite(vertCount, skinnedMeshRendererBoneCount);
#else
            computeSkinningBufferContainer = new ComputeSkinningBufferContainerSetData(vertCount, skinnedMeshRendererBoneCount);
#endif
            computeSkinningBufferContainer.StartWriting();

            var vertCounter = 0;
            var skinnedMeshCounter = 0;

            foreach (GameObject gameObject in gameObjects)
            {
                Transform rootTransform = gameObject.transform;

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int meshVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                    ResetTransforms(skinnedMeshRenderer, rootTransform);
                    FillMeshArray(skinnedMeshRenderer, meshVertexCount, vertCounter, skinnedMeshCounter);
                    vertCounter += meshVertexCount;
                    skinnedMeshCounter++;
                }
            }

            SetupBuffers(skinningShader, lastAvatarVertCount);
        }

        private void SetupBuffers(UnityEngine.ComputeShader skinningShader, int lastAvatarVertCount)
        {
            computeSkinningBufferContainer.EndWriting();
            mBones = new ComputeBuffer(ComputeShaderConstants.BONE_COUNT, Marshal.SizeOf(typeof(float4x4)), ComputeBufferType.Structured, ComputeBufferMode.Dynamic);

            cs = skinningShader;
            kernel = cs.FindKernel(ComputeShaderConstants.SKINNING_KERNEL_NAME);
            computeSkinningBufferContainer.SetBuffers(cs, kernel);
            cs.SetInt(ComputeShaderConstants.VERT_COUNT_ID, vertCount);
            cs.SetInt(ComputeShaderConstants.LAST_AVATAR_VERT_COUNT_ID, lastAvatarVertCount);
            cs.SetBuffer(kernel, ComputeShaderConstants.BONES_ID, mBones);
        }

        private void FillMeshArray(SkinnedMeshRenderer skinnedMeshRenderer, int currentMeshVertexCount, int vertexCounter, int skinnedMeshCounter)
        {
            Mesh mesh = skinnedMeshRenderer.sharedMesh;

            // HACK: We only need to do this if the avatar has _NORMALMAPS enabled on the material.
            mesh.RecalculateTangents();

            computeSkinningBufferContainer.CopyAllBuffers(mesh, currentMeshVertexCount, vertexCounter, skinnedMeshCounter);
        }

        private void SetupCounters(List<GameObject> gameObjects)
        {
            var skinnedMeshRendererCount = 0;
            foreach (GameObject gameObject in gameObjects)
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                vertCount += skinnedMeshRenderer.sharedMesh.vertexCount;
                skinnedMeshRendererCount++;
            }

            skinnedMeshRendererBoneCount = skinnedMeshRendererCount * ComputeShaderConstants.BONE_COUNT;
        }

        private void SetupMeshRenderer(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer, IObjectPool<Material> avatarMaterial, int lastAvatarVertCount, AvatarShapeComponent avatarShapeComponent)
        {
            var auxVertCounter = 0;

            foreach (GameObject gameObject in gameObjects)
            {
                foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int currentVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                    Renderer renderer = SetupMesh(skinnedMeshRenderer);
                    renderer.renderingLayerMask = 2;
                    SetupMaterial(renderer, auxVertCounter, textureArrayContainer, avatarMaterial, lastAvatarVertCount, avatarShapeComponent);
                    auxVertCounter += currentVertexCount;
                }
            }
        }

        private Renderer SetupMesh(SkinnedMeshRenderer skin)
        {
            GameObject go = skin.gameObject;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            filter.mesh = skin.sharedMesh;
            meshRenderer.material = skin.material;
            Object.Destroy(skin);
            return meshRenderer;
        }

        protected override void SetupMaterial(Renderer meshRenderer, int lastWearableVertCount, TextureArrayContainer textureArrayContainer, IObjectPool<Material> celShadingMaterial, int lastAvatarVertCount,
            AvatarShapeComponent avatarShapeComponent)
        {
            Material avatarMaterial = celShadingMaterial.Get();
            Material originalMaterial = meshRenderer.material;
            var albedoTexture = (Texture2D)originalMaterial.mainTexture;

            if (albedoTexture != null)
            {
                UsedTextureArraySlot usedIndex = textureArrayContainer.SetTexture(avatarMaterial, albedoTexture, ComputeShaderConstants.TextureArrayType.ALBEDO);
                usedTextureArraySlots.Add(usedIndex);
            }

            foreach (string keyword in ComputeShaderConstants.keywordsToCheck)
            {
                if (meshRenderer.material.IsKeywordEnabled(keyword))
                    avatarMaterial.EnableKeyword(keyword);
            }

            // HACK: We currently aren't using normal maps so we're just creating shading issues by using this variant.
            avatarMaterial.DisableKeyword("_NORMALMAP");

            //vertOutMaterial.SetColor(ComputeShaderHelpers._BaseColour_ShaderID, Color.red);
            avatarMaterial.SetInteger("_useCompute", 1);
            avatarMaterial.SetInteger(ComputeShaderConstants.LAST_AVATAR_VERT_COUNT_ID, lastWearableVertCount);
            avatarMaterial.SetInteger(ComputeShaderConstants.LAST_WEARABLE_VERT_COUNT_ID, lastAvatarVertCount);

            SetAvatarColors(avatarMaterial, originalMaterial, avatarShapeComponent);
            meshRenderer.material = avatarMaterial;
        }

        public new void Dispose()
        {
            //foreach (UsedTextureArraySlot usedTextureArraySlot in usedTextureArraySlots)
            //    textureArrayContainer.FreeTexture(usedTextureArraySlot);

            usedTextureArraySlots.Clear();
            computeSkinningBufferContainer.Dispose();
        }
    }
}
