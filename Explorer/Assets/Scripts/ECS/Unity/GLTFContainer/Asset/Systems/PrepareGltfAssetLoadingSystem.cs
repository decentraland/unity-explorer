using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;

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
        private readonly string sceneName;

        internal PrepareGltfAssetLoadingSystem(World world, IGltfContainerAssetsCache cache, string sceneName) : base(world)
        {
            this.cache = cache;
            this.sceneName = sceneName;
        }

        protected override void Update(float t)
        {
            PrepareQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>), typeof(GetAssetBundleIntention))]
        private void Prepare(in Entity entity, ref GetGltfContainerAssetIntention intention)
        {
            // Try load from cache
            if (cache.TryGet(sceneName, intention.Name, out var asset))
            {
                // construct the result immediately
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
                return;
            }

            // If not in cache, try load from asset bundle
            World.Add(entity, GetAssetBundleIntention.FromName(intention.Name));
        }
    }
}
