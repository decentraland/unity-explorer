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

        internal PrepareGltfAssetLoadingSystem(World world, IGltfContainerAssetsCache cache) : base(world)
        {
            this.cache = cache;
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
            if (cache.TryGet(intention.Hash, out GltfContainerAsset asset))
            {
                // construct the result immediately
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
                return;
            }

            // If not in cache, try load from asset bundle
            World.Add(entity, GetAssetBundleIntention.Create(typeof(GameObject), $"{intention.Hash}{PlatformUtils.GetPlatform()}", intention.Name));
        }
    }
}
