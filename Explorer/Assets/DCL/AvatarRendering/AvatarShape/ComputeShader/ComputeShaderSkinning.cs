using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.Wearables.Helpers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Profiling;
using Utility.Pool;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeShaderSkinning : CustomSkinning
    {
        private readonly List<UsedTextureArraySlot> usedTextureArraySlots;
        private ComputeSkinningBufferContainer computeSkinningBufferContainer;

        private UnityEngine.ComputeShader cs;

        //private int skinnedMeshRendererBoneCount;
        private int kernel;
        private ComputeBuffer mBones;

        private int vertCount;

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

        public override int Initialize(IReadOnlyList<CachedWearable> gameObjects, TextureArrayContainer textureArrayContainer,
            UnityEngine.ComputeShader skinningShader, IObjectPool<Material> avatarMaterialPool, int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer, AvatarShapeComponent avatarShapeComponent)
        {
            List<MeshData> meshesData = ListPool<MeshData>.Get();
            CreateMeshData(meshesData, gameObjects);

            (int vertCount, int boneCount) = SetupCounters(meshesData);
            this.vertCount = vertCount;

            SetupComputeShader(meshesData, skinningShader, lastAvatarVertCount, vertCount, boneCount);
            SetupMeshRenderer(meshesData, textureArrayContainer, avatarMaterialPool, lastAvatarVertCount, avatarShapeComponent);

            ListPool<MeshData>.Release(meshesData);

            return vertCount;
        }

        private void SetupComputeShader(IReadOnlyList<MeshData> meshesData, UnityEngine.ComputeShader skinningShader,
            int lastAvatarVertCount, int vertCount, int skinnedMeshRendererBoneCount)
        {
            Profiler.BeginSample(nameof(SetupComputeShader));

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

            for (var i = 0; i < meshesData.Count; i++)
            {
                MeshData meshData = meshesData[i];
                int meshVertexCount = meshData.Mesh.sharedMesh.vertexCount;
                ResetTransforms(meshData.Transform, meshData.RootTransform);
                FillMeshArray(meshData.Mesh.sharedMesh, meshVertexCount, vertCounter, skinnedMeshCounter);
                vertCounter += meshVertexCount;
                skinnedMeshCounter++;
            }

            SetupBuffers(skinningShader, lastAvatarVertCount, vertCount);

            Profiler.EndSample();
        }

        private void SetupBuffers(UnityEngine.ComputeShader skinningShader, int lastAvatarVertCount, int vertCount)
        {
            computeSkinningBufferContainer.EndWriting();
            mBones = new ComputeBuffer(ComputeShaderConstants.BONE_COUNT, Unsafe.SizeOf<float4x4>(), ComputeBufferType.Structured, ComputeBufferMode.Dynamic);

            cs = skinningShader;
            kernel = cs.FindKernel(ComputeShaderConstants.SKINNING_KERNEL_NAME);
            computeSkinningBufferContainer.SetBuffers(cs, kernel);
            cs.SetInt(ComputeShaderConstants.VERT_COUNT_ID, vertCount);
            cs.SetInt(ComputeShaderConstants.LAST_AVATAR_VERT_COUNT_ID, lastAvatarVertCount);
            cs.SetBuffer(kernel, ComputeShaderConstants.BONES_ID, mBones);
        }

        private void FillMeshArray(Mesh mesh, int currentMeshVertexCount, int vertexCounter, int skinnedMeshCounter)
        {
            // HACK: We only need to do this if the avatar has _NORMALMAPS enabled on the material.
            mesh.RecalculateTangents();

            computeSkinningBufferContainer.CopyAllBuffers(mesh, currentMeshVertexCount, vertexCounter, skinnedMeshCounter);
        }

        private (int vertCount, int boneCount) SetupCounters(IReadOnlyList<MeshData> meshesData)
        {
            Profiler.BeginSample(nameof(SetupCounters));

            var skinnedMeshRendererCount = 0;
            var vertCount = 0;

            for (var i = 0; i < meshesData.Count; i++)
            {
                vertCount += meshesData[i].Mesh.sharedMesh.vertexCount;
                skinnedMeshRendererCount++;
            }

            Profiler.EndSample();

            return (vertCount, skinnedMeshRendererCount * ComputeShaderConstants.BONE_COUNT);
        }

        private void SetupMeshRenderer(IReadOnlyList<MeshData> gameObjects, TextureArrayContainer textureArrayContainer,
            IObjectPool<Material> avatarMaterial, int lastAvatarVertCount, AvatarShapeComponent avatarShapeComponent)
        {
            var auxVertCounter = 0;

            for (var i = 0; i < gameObjects.Count; i++)
            {
                MeshData meshData = gameObjects[i];
                int currentVertexCount = meshData.Mesh.sharedMesh.vertexCount;
                SetupMaterial(meshData.Renderer, meshData.OriginalMaterial, auxVertCounter, textureArrayContainer, avatarMaterial, lastAvatarVertCount, avatarShapeComponent);
                auxVertCounter += currentVertexCount;
            }
        }

        private void CreateMeshData(List<MeshData> targetList, IReadOnlyList<CachedWearable> wearables)
        {
            foreach (CachedWearable cachedWearable in wearables)
            {
                GameObject instance = cachedWearable.Instance;

                using (PoolExtensions.Scope<List<MeshFilter>> pooledList = instance.GetComponentsInChildrenIntoPooledList<MeshFilter>(true))
                {
                    // From Pooled Object
                    for (var i = 0; i < pooledList.Value.Count; i++)
                    {
                        MeshFilter meshRenderer = pooledList.Value[i];
                        if (!meshRenderer.gameObject.activeSelf) continue;

                        targetList.Add(new MeshData(meshRenderer, meshRenderer.GetComponent<MeshRenderer>(), meshRenderer.transform, instance.transform,
                            cachedWearable.OriginalAsset.RendererInfos[i].Material));
                    }
                }

                using (PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = instance.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>(true))
                {
                    // From Asset Bundle
                    for (var i = 0; i < pooledList.Value.Count; i++)
                    {
                        SkinnedMeshRenderer skinnedMeshRenderer = pooledList.Value[i];
                        if (!skinnedMeshRenderer.gameObject.activeSelf) continue;

                        (MeshRenderer, MeshFilter) tuple = SetupMesh(skinnedMeshRenderer);

                        targetList.Add(new MeshData(tuple.Item2, tuple.Item1, tuple.Item1.transform, instance.transform,
                            cachedWearable.OriginalAsset.RendererInfos[i].Material));
                    }
                }
            }
        }

        private (MeshRenderer, MeshFilter) SetupMesh(SkinnedMeshRenderer skin)
        {
            GameObject go = skin.gameObject;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            filter.mesh = skin.sharedMesh;
            meshRenderer.material = skin.material;
            meshRenderer.renderingLayerMask = 2;
            Object.Destroy(skin);
            return (meshRenderer, filter);
        }

        protected override void SetupMaterial(Renderer meshRenderer, Material originalMaterial, int lastWearableVertCount,
            TextureArrayContainer textureArrayContainer, IObjectPool<Material> celShadingMaterial, int lastAvatarVertCount,
            AvatarShapeComponent avatarShapeComponent)
        {
            Material avatarMaterial = celShadingMaterial.Get();
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

        private readonly struct MeshData
        {
            public readonly MeshFilter Mesh;
            public readonly Renderer Renderer;
            public readonly Material OriginalMaterial;
            public readonly Transform Transform;
            public readonly Transform RootTransform;

            public MeshData(MeshFilter mesh, Renderer renderer, Transform transform, Transform rootTransform, Material originalMaterial)
            {
                Mesh = mesh;
                Transform = transform;
                RootTransform = rootTransform;
                OriginalMaterial = originalMaterial;
                Renderer = renderer;
            }
        }
    }
}
