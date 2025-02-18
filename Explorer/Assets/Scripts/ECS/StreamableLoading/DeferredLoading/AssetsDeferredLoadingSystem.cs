﻿using Arch.Core;
using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.Textures;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading
{
    /// <summary>
    ///     Weighs asset bundles and textures against each other according to their partition
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(PrepareAssetBundleLoadingParametersSystem))]
    [UpdateBefore(typeof(LoadSceneTextureSystem))]
    [UpdateBefore(typeof(LoadAudioClipSystem))]
    [UpdateBefore(typeof(LoadNFTShapeSystem))]
    [UpdateBefore(typeof(LoadAssetBundleSystem))]
    public partial class AssetsDeferredLoadingSystem : DeferredLoadingSystem
    {
        private static readonly QueryDescription[] COMPONENT_HANDLERS;

        static AssetsDeferredLoadingSystem()
        {
            COMPONENT_HANDLERS = new[]
            {
                CreateQuery<GetAssetBundleIntention, AssetBundleData>(),
                CreateQuery<GetGLTFIntention, GLTFData>(),
                CreateQuery<GetTextureIntention, Texture2DData>(),
                CreateQuery<GetNFTShapeIntention, Texture2DData>(),
                CreateQuery<GetAudioClipIntention, AudioClipData>(),
            };
        }

        public AssetsDeferredLoadingSystem(World world, IReleasablePerformanceBudget releasablePerformanceLoadingBudget, IPerformanceBudget memoryBudget)
            : base(world, COMPONENT_HANDLERS, releasablePerformanceLoadingBudget, memoryBudget)
        {
        }

    }
}
