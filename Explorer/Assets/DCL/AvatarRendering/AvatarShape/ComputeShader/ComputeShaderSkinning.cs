using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DCL.AvatarRendering.AvatarShape.Helpers;
using ECS.Unity.Textures.Components;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeShaderSkinning : CustomSkinning
    {
        public override AvatarCustomSkinningComponent Initialize(IList<CachedWearable> gameObjects,
            UnityEngine.ComputeShader skinningShader, IAvatarMaterialPoolHandler avatarMaterialPool, AvatarShapeComponent avatarShapeComponent,
            Dictionary<string, Texture> facialFeatureTexture)
        {
            List<MeshData> meshesData = ListPool<MeshData>.Get();

            CreateMeshData(meshesData, gameObjects);

            (int vertCount, int boneCount) = SetupCounters(meshesData);

            AvatarCustomSkinningComponent.Buffers buffers = SetupComputeShader(meshesData, skinningShader, vertCount, boneCount);
            List<AvatarCustomSkinningComponent.MaterialSetup> materialSetups = SetupMeshRenderer(meshesData, avatarMaterialPool, avatarShapeComponent, facialFeatureTexture);

            ListPool<MeshData>.Release(meshesData);

            return new AvatarCustomSkinningComponent(vertCount, buffers, materialSetups, skinningShader);
        }
        public override void ComputeSkinning(NativeArray<float4x4> bonesResult, ref AvatarCustomSkinningComponent skinning)
        {
            skinning.buffers.bones.SetData(bonesResult);
            skinning.computeShaderInstance.Dispatch(skinning.buffers.kernel, (skinning.vertCount / 64) + 1, 1, 1);

            //Note (Juani): According to Unity, BeginWrite/EndWrite works better than SetData. But we got inconsitent result using ComputeBufferMode.SubUpdates
            //Ash machine (AMD) worked way worse than mine (NVidia). So, we are back to SetData with a ComputeBufferMode.Dynamic, which works well for both.
            //https://docs.unity3d.com/2020.1/Documentation/ScriptReference/ComputeBuffer.BeginWrite.html
            /*NativeArray<float4x4> bonesIn = mBones.BeginWrite<float4x4>(0, ComputeShaderConstants.BONE_COUNT);
            NativeArray<float4x4>.Copy(bonesResult, 0, bonesIn, 0, ComputeShaderConstants.BONE_COUNT);
            mBones.EndWrite<float4x4>(ComputeShaderConstants.BONE_COUNT);*/
        }

        private static ComputeSkinningBufferContainer CreateBufferContainer(int vertCount, int skinnedMeshRendererBoneCount)
        {
            //Note (Juani): Using too many BeginWrite in Mac caused a crash. So I ve set up this switch that changes the way in which we
            //set up the buffers depending on the platform

#if UNITY_STANDALONE_WIN
            return new ComputeSkinningBufferContainerWrite(vertCount, skinnedMeshRendererBoneCount);
#else
            return new ComputeSkinningBufferContainerSetData(vertCount, skinnedMeshRendererBoneCount);
#endif
        }

        private AvatarCustomSkinningComponent.Buffers SetupComputeShader(IReadOnlyList<MeshData> meshesData, UnityEngine.ComputeShader skinningShader, int vertCount, int skinnedMeshRendererBoneCount)
        {
            Profiler.BeginSample(nameof(SetupComputeShader));

            ComputeSkinningBufferContainer computeSkinningBufferContainer = CreateBufferContainer(vertCount, skinnedMeshRendererBoneCount);

            computeSkinningBufferContainer.StartWriting();

            var vertCounter = 0;
            var skinnedMeshCounter = 0;

            for (var i = 0; i < meshesData.Count; i++)
            {
                MeshData meshData = meshesData[i];
                int meshVertexCount = meshData.Mesh.sharedMesh.vertexCount;
                ResetTransforms(meshData.Transform, meshData.RootTransform);
                FillMeshArray(meshData.Mesh.sharedMesh, meshVertexCount, vertCounter, skinnedMeshCounter, computeSkinningBufferContainer);
                vertCounter += meshVertexCount;
                skinnedMeshCounter++;
            }

            AvatarCustomSkinningComponent.Buffers buffers = SetupBuffers(computeSkinningBufferContainer, skinningShader, vertCount);
            buffers.computeSkinningBufferContainer = computeSkinningBufferContainer;

            Profiler.EndSample();

            return buffers;
        }

        private AvatarCustomSkinningComponent.Buffers SetupBuffers(
            ComputeSkinningBufferContainer computeSkinningBufferContainer,
            UnityEngine.ComputeShader cs, int vertCount)
        {
            computeSkinningBufferContainer.EndWriting();
            var mBones = new ComputeBuffer(ComputeShaderConstants.BONE_COUNT, Unsafe.SizeOf<float4x4>(), ComputeBufferType.Structured, ComputeBufferMode.Dynamic);

            int kernel = cs.FindKernel(ComputeShaderConstants.SKINNING_KERNEL_NAME);
            computeSkinningBufferContainer.SetBuffers(cs, kernel);
            cs.SetInt(ComputeShaderConstants.VERT_COUNT_ID, vertCount);
            cs.SetBuffer(kernel, ComputeShaderConstants.BONES_ID, mBones);

            return new AvatarCustomSkinningComponent.Buffers(mBones, kernel);
        }

        private void FillMeshArray(Mesh mesh, int currentMeshVertexCount, int vertexCounter, int skinnedMeshCounter, ComputeSkinningBufferContainer computeSkinningBufferContainer)
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

        private List<AvatarCustomSkinningComponent.MaterialSetup> SetupMeshRenderer(IReadOnlyList<MeshData> gameObjects,
            IAvatarMaterialPoolHandler avatarMaterial, AvatarShapeComponent avatarShapeComponent, Dictionary<string, Texture> facilFeatureTexture)
        {
            var auxVertCounter = 0;

            List<AvatarCustomSkinningComponent.MaterialSetup> list = AvatarCustomSkinningComponent.USED_SLOTS_POOL.Get();

            for (var i = 0; i < gameObjects.Count; i++)
            {
                MeshData meshData = gameObjects[i];
                int currentVertexCount = meshData.Mesh.sharedMesh.vertexCount;
                list.Add(SetupMaterial(meshData.Renderer, meshData.OriginalMaterial, auxVertCounter, avatarMaterial, avatarShapeComponent, facilFeatureTexture));
                auxVertCounter += currentVertexCount;
            }

            return list;
        }

        private void CreateMeshData(List<MeshData> targetList, IList<CachedWearable> wearables)
        {
            for (var i = 0; i < wearables.Count; i++)
            {
                CachedWearable cachedWearable = wearables[i];
                GameObject instance = cachedWearable.Instance;

                using (PoolExtensions.Scope<List<Renderer>> pooledList = instance.GetComponentsInChildrenIntoPooledList<Renderer>(true))
                {
                    for (var j = 0; j < pooledList.Value.Count; j++)
                    {
                        Renderer meshRenderer = pooledList.Value[j];
                        if (!meshRenderer.gameObject.activeSelf) continue;

                        if (meshRenderer is SkinnedMeshRenderer renderer)
                        {
                            // From Asset Bundle
                            (MeshRenderer, MeshFilter) tuple = SetupMesh(renderer);

                            cachedWearable.Renderers.Add(tuple.Item1);

                            targetList.Add(new MeshData(tuple.Item2, tuple.Item1, tuple.Item1.transform, instance.transform,
                                cachedWearable.OriginalAsset.RendererInfos[j].Material));
                        }
                        else
                        {
                            cachedWearable.Renderers.Add(meshRenderer);

                            // From Pooled Object
                            targetList.Add(new MeshData(meshRenderer.GetComponent<MeshFilter>(), meshRenderer, meshRenderer.transform, instance.transform,
                                cachedWearable.OriginalAsset.RendererInfos[j].Material));
                        }
                    }
                }

                wearables[i] = cachedWearable;
            }
        }

        private (MeshRenderer, MeshFilter) SetupMesh(SkinnedMeshRenderer skin)
        {
            GameObject go = skin.gameObject;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.mesh = skin.sharedMesh;

            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.renderingLayerMask = 2;

            meshRenderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 5);
            Object.Destroy(skin);
            return (meshRenderer, filter);
        }

        private (Material, TextureArraySlot?[], int) DoFacialFeature(IAvatarMaterialPoolHandler poolHandler, Texture replacementTexture, Renderer meshRenderer, AvatarShapeComponent avatarShapeComponent)
        {
            int resolution = replacementTexture.width;
            int materialIndexInPool = TextureArrayConstants.SHADERID_DCL_FACIAL_FEATURES * resolution;
            var poolMaterialSetup = poolHandler.GetMaterialPool(materialIndexInPool);
            var avatarMaterial = poolMaterialSetup.Pool.Get();
            var slots = poolMaterialSetup.TextureArrayContainer.SetTexturesFromOriginalMaterial(TextureArrayConstants.MAINTEX_ORIGINAL_TEXTURE, (Texture2D)replacementTexture, avatarMaterial);
            return (avatarMaterial, slots, materialIndexInPool);
        }

        private protected override AvatarCustomSkinningComponent.MaterialSetup SetupMaterial(Renderer meshRenderer, Material originalMaterial, int lastWearableVertCount, IAvatarMaterialPoolHandler poolHandler, AvatarShapeComponent avatarShapeComponent, Dictionary<string, Texture> facialFeatures)
        {
            var slots = Array.Empty<TextureArraySlot?>();
            Material avatarMaterial = null;
            int materialIndexInPool = -1;

            if (meshRenderer.gameObject.name.Contains("eyes", StringComparison.OrdinalIgnoreCase))
            {
                (avatarMaterial, slots, materialIndexInPool) = DoFacialFeature(poolHandler, facialFeatures[WearablesConstants.Categories.EYES], meshRenderer, avatarShapeComponent);
                avatarMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                avatarMaterial.SetInt("_ZWriteMode", 0);
            }
            else if (meshRenderer.gameObject.name.Contains("eyebrows", StringComparison.OrdinalIgnoreCase))
            {
                (avatarMaterial, slots, materialIndexInPool) = DoFacialFeature(poolHandler, facialFeatures[WearablesConstants.Categories.EYEBROWS], meshRenderer, avatarShapeComponent);
                avatarMaterial.SetColor("_BaseColor", avatarShapeComponent.HairColor);
                avatarMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                avatarMaterial.SetInt("_ZWriteMode", 0);
            }
            else if (meshRenderer.gameObject.name.Contains("mouth", StringComparison.OrdinalIgnoreCase))
            {
                (avatarMaterial, slots, materialIndexInPool) = DoFacialFeature(poolHandler, facialFeatures[WearablesConstants.Categories.MOUTH], meshRenderer, avatarShapeComponent);
                avatarMaterial.SetColor("_BaseColor", avatarShapeComponent.SkinColor);
                avatarMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                avatarMaterial.SetInt("_ZWriteMode", 0);
            }
            else
            {
                var tex = originalMaterial.GetTexture(TextureArrayConstants.MAINTEX_ORIGINAL_TEXTURE) as Texture2D;
                int resolution = tex != null ? tex.width : TextureArrayConstants.MAIN_TEXTURE_RESOLUTION;

                materialIndexInPool = TextureArrayConstants.SHADERID_DCL_TOON * resolution;
                var poolMaterialSetup = poolHandler.GetMaterialPool(materialIndexInPool);
                avatarMaterial = poolMaterialSetup.Pool.Get();

                if (originalMaterial.IsKeywordEnabled("_ALPHATEST_ON") || originalMaterial.GetFloat("_AlphaClip") > 0)
                {
                    avatarMaterial.EnableKeyword("_IS_CLIPPING_MODE");
                    avatarMaterial.DisableKeyword("_IS_CLIPPING_TRANSMODE");
                    Color baseColour = originalMaterial.GetColor("_BaseColor");
                    avatarMaterial.SetFloat("_Tweak_transparency", 1.0f - baseColour.a);
                    avatarMaterial.SetFloat("_Clipping_Level", originalMaterial.GetFloat("_Cutoff"));
                    avatarMaterial.SetInt("_ZWriteMode", 1);
                    avatarMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
                if (originalMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || originalMaterial.GetFloat("_Surface") > 0)
                {
                    avatarMaterial.DisableKeyword("_IS_CLIPPING_MODE");
                    avatarMaterial.EnableKeyword("_IS_CLIPPING_TRANSMODE");
                    Color baseColour = originalMaterial.GetColor("_BaseColor");
                    avatarMaterial.SetFloat("_Tweak_transparency", 0.0f - (1.0f - baseColour.a));
                    avatarMaterial.SetFloat("_Clipping_Level", originalMaterial.GetFloat("_Cutoff"));
                    avatarMaterial.SetInt("_ZWriteMode", 0);
                    avatarMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }

                slots = poolMaterialSetup.TextureArrayContainer.SetTexturesFromOriginalMaterial(originalMaterial, avatarMaterial);
            }

            avatarMaterial.SetInteger(ComputeShaderConstants.LAST_WEARABLE_VERT_COUNT_ID, lastWearableVertCount);
            SetAvatarColors(avatarMaterial, originalMaterial, avatarShapeComponent);
            meshRenderer.material = avatarMaterial;

            return new AvatarCustomSkinningComponent.MaterialSetup(slots, avatarMaterial, materialIndexInPool);
        }

        public override void SetVertOutRegion(FixedComputeBufferHandler.Slice region, ref AvatarCustomSkinningComponent skinningComponent)
        {
            skinningComponent.VertsOutRegion = region;

            skinningComponent.computeShaderInstance.SetInt(ComputeShaderConstants.LAST_AVATAR_VERT_COUNT_ID, region.StartIndex);

            for (var i = 0; i < skinningComponent.materials.Count; i++)
                skinningComponent.materials[i].usedMaterial.SetInteger(ComputeShaderConstants.LAST_AVATAR_VERT_COUNT_ID, region.StartIndex);
        }
    }
}
