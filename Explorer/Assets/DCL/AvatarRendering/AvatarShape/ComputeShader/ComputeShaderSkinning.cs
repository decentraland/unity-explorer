using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        /// <summary>
        ///     Original-material texture slot that feeds the toon shader's tangent-space normal-map
        ///     array. The normal handler in <see cref="TextureArrayContainerFactory" /> is registered on
        ///     <c>BUMP_MAP_ORIGINAL_TEXTURE_ID</c> (<c>_BumpMap</c>) — see
        ///     TextureArrayContainerFactory.cs (regular + raw-GLTF mappings) — and
        ///     <c>TextureArrayContainer.SetTexturesFromOriginalMaterial</c> reads exactly that slot when
        ///     building <c>_NormalMapArr</c>. This is therefore the ONLY property that decides whether a
        ///     mesh's rendered material samples a tangent-space normal map, so it is the property the
        ///     tangent-recompute gate must key off. (<c>_NormalMap</c> / NORMAL_MAP_ORIGINAL_TEXTURE_ID is
        ///     dead in production and must NOT be used here.)
        /// </summary>
        public static readonly int TANGENT_SOURCE_TEXTURE_ID = TextureArrayConstants.BUMP_MAP_ORIGINAL_TEXTURE_ID;

        /// <summary>
        ///     Whether a mesh needs a <see cref="Mesh.RecalculateTangents" /> pass before compute skinning.
        ///     Tangents are only consumed when the rendered material samples a tangent-space normal map,
        ///     which for wearable/body meshes is sourced from the original material's
        ///     <see cref="TANGENT_SOURCE_TEXTURE_ID" /> (<c>_BumpMap</c>) slot. Facial-feature meshes render
        ///     through the DCL_FACIAL_FEATURES shader (textures come from replacement maps, NOT the original
        ///     material — see <c>AvatarMaterialConfiguration.DoFacialFeature</c>), so the _BumpMap probe is
        ///     not meaningful for them; they retain the original unconditional recompute.
        /// </summary>
        public static bool MeshNeedsTangents(Renderer renderer, Material originalMaterial)
        {
            if (renderer != null && AvatarMaterialConfiguration.IsFacialFeature(renderer))
                return true;

            return originalMaterial != null
                   && originalMaterial.GetTexture(TANGENT_SOURCE_TEXTURE_ID) != null;
        }

        public override AvatarCustomSkinningComponent Initialize(IList<CachedAttachment> gameObjects,
            UnityEngine.ComputeShader skinningShader, IAvatarMaterialPoolHandler avatarMaterialPool, AvatarShapeComponent avatarShapeComponent,
            in FacialFeaturesTextures facialFeatureTexture, int boneCount)
        {
            List<MeshData> meshesData = ListPool<MeshData>.Get();

            CreateMeshData(meshesData, gameObjects);

            (int vertCount, int totalBoneBufferCount) = SetupCounters(meshesData, boneCount);

            AvatarCustomSkinningComponent.Buffers buffers = SetupComputeShader(meshesData, skinningShader, vertCount, totalBoneBufferCount, boneCount);
            List<AvatarCustomSkinningComponent.MaterialSetup> materialSetups = SetupMeshRenderer(meshesData, avatarMaterialPool, avatarShapeComponent, facialFeatureTexture);

            Bounds totalBounds =  CalculateLocalBoundsFromMeshes(meshesData);

            ListPool<MeshData>.Release(meshesData);

            return new AvatarCustomSkinningComponent(vertCount, boneCount, buffers, materialSetups, skinningShader, totalBounds);
        }

        private AvatarCustomSkinningComponent.Buffers SetupComputeShader(IReadOnlyList<MeshData> meshesData, UnityEngine.ComputeShader skinningShader, int vertCount, int skinnedMeshRendererBoneCount, int boneCount)
        {
            Profiler.BeginSample(nameof(SetupComputeShader));

            ComputeSkinningBufferContainer computeSkinningBufferContainer = ComputeSkinningBufferContainer.New(vertCount, skinnedMeshRendererBoneCount);

            computeSkinningBufferContainer.StartWriting();

            var vertCounter = 0;
            var skinnedMeshCounter = 0;

            for (var i = 0; i < meshesData.Count; i++)
            {
                MeshData meshData = meshesData[i];
                int meshVertexCount = meshData.Mesh.sharedMesh.vertexCount;
                ResetTransforms(meshData.Transform, meshData.RootTransform);
                bool needsTangents = MeshNeedsTangents(meshData.Renderer, meshData.OriginalMaterial);
                FillMeshArray(meshData.Mesh.sharedMesh, meshVertexCount, vertCounter, skinnedMeshCounter, computeSkinningBufferContainer, boneCount, meshData.SpringBoneOffset, needsTangents);
                vertCounter += meshVertexCount;
                skinnedMeshCounter++;
            }

            AvatarCustomSkinningComponent.Buffers buffers = SetupBuffers(computeSkinningBufferContainer, skinningShader, vertCount, boneCount);
            buffers.AssignBuffer(computeSkinningBufferContainer);

            Profiler.EndSample();

            return buffers;
        }

        private AvatarCustomSkinningComponent.Buffers SetupBuffers(
            ComputeSkinningBufferContainer computeSkinningBufferContainer,
            UnityEngine.ComputeShader cs, int vertCount, int boneCount)
        {
            computeSkinningBufferContainer.EndWriting();
            var mBones = new ComputeBuffer(boneCount, Unsafe.SizeOf<float4x4>(), ComputeBufferType.Structured, ComputeBufferMode.Dynamic);

            int kernel = cs.FindKernel(ComputeShaderConstants.SKINNING_KERNEL_NAME);
            computeSkinningBufferContainer.SetBuffers(cs, kernel);
            cs.SetInt(ComputeShaderConstants.VERT_COUNT_ID, vertCount);
            cs.SetBuffer(kernel, ComputeShaderConstants.BONES_ID, mBones);

            return new AvatarCustomSkinningComponent.Buffers(mBones, kernel);
        }

        private void FillMeshArray(Mesh mesh, int currentMeshVertexCount, int vertexCounter, int skinnedMeshCounter, ComputeSkinningBufferContainer computeSkinningBufferContainer, int boneCount, int springBoneOffset, bool needsTangents)
        {
            // RecalculateTangents is an O(vertexCount) main-thread pass that also allocates transient
            // arrays and forces a mesh CPU->GPU re-upload. Tangents are only sampled by the rendered
            // material when it has a normal map, sourced from the original material's _BumpMap slot
            // (see MeshNeedsTangents / TANGENT_SOURCE_TEXTURE_ID). Skip the pass for meshes with no
            // normal map to cut the crowd-spawn hitch.
            if (needsTangents)
                mesh.RecalculateTangents();

            computeSkinningBufferContainer.CopyAllBuffers(mesh, currentMeshVertexCount, vertexCounter, skinnedMeshCounter, boneCount, springBoneOffset);
        }

        private (int vertCount, int totalBoneBufferCount) SetupCounters(IReadOnlyList<MeshData> meshesData, int boneCount)
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

            return (vertCount, skinnedMeshRendererCount * boneCount);
        }

        private List<AvatarCustomSkinningComponent.MaterialSetup> SetupMeshRenderer(IReadOnlyList<MeshData> gameObjects,
            IAvatarMaterialPoolHandler avatarMaterial, AvatarShapeComponent avatarShapeComponent, in FacialFeaturesTextures facilFeatureTexture)
        {
            var auxVertCounter = 0;

            List<AvatarCustomSkinningComponent.MaterialSetup> list = AvatarCustomSkinningComponent.USED_SLOTS_POOL.Get();

            for (var i = 0; i < gameObjects.Count; i++)
            {
                MeshData meshData = gameObjects[i];
                int currentVertexCount = meshData.Mesh.sharedMesh.vertexCount;
                list.Add(SetupMaterial(meshData.Renderer, meshData.OriginalMaterial, auxVertCounter, avatarMaterial, avatarShapeComponent, facilFeatureTexture));
                auxVertCounter += currentVertexCount;

                if (avatarShapeComponent.ShowOnlyWearables)
                {
                    string name = meshData.OriginalMaterial.name;

                    if (name.Contains(ComputeShaderConstants.SKIN_MATERIAL_NAME, StringComparison.OrdinalIgnoreCase)
                        || name.Contains(ComputeShaderConstants.HAIR_MATERIAL_NAME, StringComparison.OrdinalIgnoreCase))
                        meshData.Renderer.enabled = false;
                }
                else
                    meshData.Renderer.enabled = true;
            }

            return list;
        }


        private void CreateMeshData(List<MeshData> targetList, IList<CachedAttachment> wearables)
        {
            // Track cumulative spring bone count so each wearable's BoneWeight indices
            // can be offset to the correct slot in the global bone matrix buffer.
            var springBoneOffset = 0;

            for (var i = 0; i < wearables.Count; i++)
            {
                CachedAttachment cachedWearable = wearables[i];
                GameObject instance = cachedWearable.Instance;

                using (PoolExtensions.Scope<List<Renderer>> pooledList = instance.GetComponentsInChildrenIntoPooledList<Renderer>(true))
                {
                    for (var j = 0; j < pooledList.Value.Count; j++)
                    {
                        Renderer meshRenderer = pooledList.Value[j];
                        if (!meshRenderer.gameObject.activeSelf) continue;

                        if (j < 0 || j >= cachedWearable.OriginalAsset.RendererInfos.Count)
                        {
                            ReportHub.LogError(ReportCategory.AVATAR, $"RendererInfos.Count ({pooledList.Value.Count}) is different than pooledList.Value.Count ({pooledList.Value.Count})");
                            continue;
                        }

                        Material originalMaterial = cachedWearable.OriginalAsset.RendererInfos[j].Material;

                        if (meshRenderer is SkinnedMeshRenderer renderer)
                        {
                            // From Asset Bundle
                            (MeshRenderer, MeshFilter) tuple = SetupMesh(renderer);

                            cachedWearable.Renderers.Add(tuple.Item1);

                            targetList.Add(new MeshData(tuple.Item2, tuple.Item1, tuple.Item1.transform, instance.transform,
                                originalMaterial, springBoneOffset));
                        }
                        else
                        {
                            cachedWearable.Renderers.Add(meshRenderer);

                            // From Pooled Object
                            targetList.Add(new MeshData(meshRenderer.GetComponent<MeshFilter>(), meshRenderer, meshRenderer.transform, instance.transform,
                                originalMaterial, springBoneOffset));
                        }
                    }
                }

                springBoneOffset += cachedWearable.SpringBones.Length;
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

        private protected override AvatarCustomSkinningComponent.MaterialSetup SetupMaterial(Renderer meshRenderer, Material originalMaterial, int lastWearableVertCount, IAvatarMaterialPoolHandler poolHandler,
            AvatarShapeComponent avatarShapeComponent, in FacialFeaturesTextures facialFeaturesTextures) =>
            AvatarMaterialConfiguration.SetupMaterial(meshRenderer, originalMaterial, lastWearableVertCount, poolHandler, avatarShapeComponent, facialFeaturesTextures);

        /// <summary>
        /// Checks the bounds of a list of meshes and computes the bounding box that contains them all.
        /// </summary>
        /// <param name="meshes">A list of meshes.</param>
        /// <returns>A bounding box that contains all the meshes.</returns>
        private static Bounds CalculateLocalBoundsFromMeshes(List<MeshData> meshes)
        {
            Vector3 maxCorner = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 minCorner = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

            for (int i = 0; i < meshes.Count; ++i)
            {
                Bounds meshBounds = meshes[i].Mesh.sharedMesh.bounds;

                if (maxCorner.x < meshBounds.max.x)
                    maxCorner.x = meshBounds.max.x;

                if (maxCorner.y < meshBounds.max.y)
                    maxCorner.y = meshBounds.max.y;

                if (maxCorner.z < meshBounds.max.z)
                    maxCorner.z = meshBounds.max.z;

                if (minCorner.x > meshBounds.min.x)
                    minCorner.x = meshBounds.min.x;

                if (minCorner.y > meshBounds.min.y)
                    minCorner.y = meshBounds.min.y;

                if (minCorner.z > meshBounds.min.z)
                    minCorner.z = meshBounds.min.z;
            }

            Vector3 size = maxCorner - minCorner;
            return new Bounds(minCorner + size * 0.5f, size);
        }
    }
}
