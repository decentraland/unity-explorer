using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Utility;
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

            if (options is {LocalSceneDevelopment: true, UseRemoteAssetBundles: true })
                FallbackToRawGltfQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>), typeof(GetAssetBundleIntention), typeof(GetGLTFIntention))]
        private void Prepare(in Entity entity, ref GetGltfContainerAssetIntention intention)
        {
            bool allowCaching = options is { LocalSceneDevelopment: false, PreviewingBuilderCollection: false };

            // Try loading from the cache
            if (allowCaching && cache.TryGet(intention.Hash, out GltfContainerAsset? asset))
            {
                // Construct the result immediately
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
                return;
            }

            bool loadRawGltf = options.PreviewingBuilderCollection || options is { LocalSceneDevelopment: true, UseRemoteAssetBundles: false };
            if (loadRawGltf)
                World.Add(entity, GetGLTFIntention.Create(intention.Name, intention.Hash));
            else
                World.Add(entity, GetAssetBundleIntention.Create(typeof(GameObject), $"{intention.Hash}{PlatformUtils.GetCurrentPlatform()}", intention.Name));
        }

        /// <summary>
        ///     AB loading failed in LSD mode — fall back to loading raw GLTF from the local content server.
        /// </summary>
        [Query]
        [None(typeof(GetGLTFIntention))]
        private void FallbackToRawGltf(in Entity entity, ref GetGltfContainerAssetIntention intention, ref StreamableLoadingResult<GltfContainerAsset> result)
        {
            if (result.Succeeded) return;

            // Tried to load remotely, the AB is missing, then try to load locally
            sceneData.SceneContent.SwitchToLocal(intention.Name);
            World.Remove<StreamableLoadingResult<GltfContainerAsset>>(entity);
            World.Add(entity, GetGLTFIntention.Create(intention.Name, intention.Hash));
        }

        public struct Options
        {
            public bool LocalSceneDevelopment;
            public bool UseRemoteAssetBundles;
            public bool PreviewingBuilderCollection;
        }
    }
}
