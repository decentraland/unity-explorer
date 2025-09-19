using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using UnityEngine;
using Utility;

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
        private readonly Options options;

        internal PrepareGltfAssetLoadingSystem(World world, IGltfContainerAssetsCache cache, Options options) : base(world)
        {
            this.cache = cache;
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
            bool allowCaching = options is { LocalSceneDevelopment: false, PreviewingBuilderCollection: false };

            // Try loading from the cache
            if (allowCaching && cache.TryGet(intention.Hash, out GltfContainerAsset? asset))
            {
                // Construct the result immediately
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
                return;
            }

            bool loadRawGltf = options.PreviewingBuilderCollection || options is { LocalSceneDevelopment: true, UseRemoveAssetBundles: false };
            if (loadRawGltf)
                World.Add(entity, GetGLTFIntention.Create(intention.Name, intention.Hash));
            else
                World.Add(entity, GetAssetBundleIntention.Create(typeof(GameObject), $"{intention.Hash}{PlatformUtils.GetCurrentPlatform()}", intention.Name));
        }

        public struct Options
        {
            public bool LocalSceneDevelopment;

            public bool UseRemoveAssetBundles;

            public bool PreviewingBuilderCollection;
        }
    }
}
