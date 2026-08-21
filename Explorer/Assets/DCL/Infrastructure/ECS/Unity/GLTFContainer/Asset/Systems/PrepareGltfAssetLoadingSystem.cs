using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    /// <summary>
    ///     Prepares to load <see cref="GltfContainerAsset" /> from either source
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateBefore(typeof(LoadAssetBundleSystem))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class PrepareGltfAssetLoadingSystem : BaseUnityLoopSystem
    {
        private readonly IGltfContainerAssetsCache cache;
        private readonly ISceneData sceneData;
        private readonly Options options;

        internal PrepareGltfAssetLoadingSystem(World world, IGltfContainerAssetsCache cache, ISceneData sceneData, Options options) : base(world)
        {
            this.cache = cache;
            this.sceneData = sceneData;
            this.options = options;
        }

        protected override void Update(float t)
        {
            PrepareQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>), typeof(GetAssetBundleIntention), typeof(GetGLTFIntention))]
        private void Prepare(in Entity entity, ref GetGltfContainerAssetIntention intention)
        {
            // Builder preview bypasses the cache so creators always see the latest collection state.
            bool allowCaching = !options.PreviewingBuilderCollection;

            // Try loading from the cache
            if (allowCaching && cache.TryGet(intention.CacheKey, out GltfContainerAsset? asset))
            {
                // In LSD a raw-GLTF asset is only reusable while the external files its import fetched
                // (textures, buffers) still resolve to the URLs it was imported from; a hot reload can
                // republish one of them under a new content hash while the GLTF's own hash — the cache
                // key — stays the same. TryGet pops the pooled instance, so a stale one must be disposed
                // here, and the remaining instances under the key are equally stale.
                if (options.LocalSceneDevelopment && IsStaleRawGltf(asset!))
                {
                    asset!.Dispose();
                    cache.Remove(intention.CacheKey);
                }
                else
                {
                    // Construct the result immediately
                    World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
                    return;
                }
            }

            bool loadRawGltf = options.PreviewingBuilderCollection;

            if (options.LocalSceneDevelopment)
            {
                if (options.UseRemoteAssetBundles)
                    loadRawGltf |= sceneData.SceneContent.IsRawAsset(intention.Name);
                else if (options.UseLocalAssetBundles)

                    // Whole-scene degrade: when the manifest could not be fetched from the local
                    // asset-bundle server every AB request would dead-end, so load raw GLTFs instead.
                    loadRawGltf |= sceneData.SceneEntityDefinition.assetBundleManifestVersion is not { assetBundleManifestRequestFailed: false };
                else
                    loadRawGltf = true;
            }

            if (loadRawGltf)
                World.Add(entity, GetGLTFIntention.Create(intention.Name, intention.Hash));
            else
            {
                AssetBundleManifestVersion abManifest = sceneData.SceneEntityDefinition.AssetBundleManifestVersionOrFailed;

                World.Add(entity, GetAssetBundleIntention.Create(typeof(GameObject),
                    abManifest.GetCdnRequestHash(intention.Hash),
                    intention.Name,
                    abManifest,
                    sceneData.SceneEntityDefinition.id ?? string.Empty));
            }
        }

        private bool IsStaleRawGltf(GltfContainerAsset asset) =>
            asset.AssetData is GLTFData gltfData && !GltfExternalDependency.AreUpToDate(gltfData.ExternalDependencies, sceneData.SceneContent);

        public struct Options
        {
            public bool LocalSceneDevelopment;
            public bool UseRemoteAssetBundles;
            public bool UseLocalAssetBundles;
            public bool PreviewingBuilderCollection;
        }
    }
}
