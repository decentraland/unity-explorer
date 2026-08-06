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
            // LSD reuse is safe within a session and is invalidated on `/reload` by ECSReloadScene's
            // eager cache drain — required because the LSD dev server's hash is path-based, not content-based.
            bool allowCaching = !options.PreviewingBuilderCollection;

            // Try loading from the cache
            if (allowCaching && cache.TryGet(intention.CacheKey, out GltfContainerAsset? asset))
            {
                // Construct the result immediately
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
                return;
            }

            // Untrusted catalysts never use asset bundles: every scene asset loads as a raw GLTF from the realm.
            bool loadRawGltf = options.PreviewingBuilderCollection || options.ForceRawGltf;

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

        public struct Options
        {
            public bool LocalSceneDevelopment;
            public bool UseRemoteAssetBundles;
            public bool UseLocalAssetBundles;
            public bool PreviewingBuilderCollection;

            /// <summary>Forces every asset to load as a raw GLTF — set when the session runs against an untrusted catalyst.</summary>
            public bool ForceRawGltf;
        }
    }
}
