using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Ipfs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiles;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.GlobalPartitioning
{
    /// <summary>
    ///     Weighs asset, definitions and scenes loading against each other according to their partition in the global world
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [UpdateBefore(typeof(LoadSceneDefinitionListSystem))]
    [UpdateBefore(typeof(LoadSceneSystem))]
    [UpdateBefore(typeof(LoadSceneDefinitionSystem))]
    [UpdateBefore(typeof(LoadWearableAssetBundleManifestSystem))]
    [UpdateBefore(typeof(LoadGlobalAssetBundleSystem))]
    [UpdateBefore(typeof(LoadWearablesDTOByPointersSystem))]
    [UpdateBefore(typeof(LoadWearablesByParamSystem))]
    public partial class GlobalDeferredLoadingSystem : DeferredLoadingSystem
    {
        private static readonly QueryDescription[] COMPONENT_HANDLERS;

        static GlobalDeferredLoadingSystem()
        {
            COMPONENT_HANDLERS = new[]
            {
                CreateQuery<GetSceneDefinitionList, SceneDefinitions>(),
                CreateQuery<GetSceneDefinition, SceneEntityDefinition>(),
                CreateQuery<GetSceneFacadeIntention, ISceneFacade>(),
                CreateQuery<GetWearableDTOByPointersIntention, WearablesDTOList>(),
                CreateQuery<GetWearableByParamIntention, IWearable[]>(),
                CreateQuery<GetWearableAssetBundleManifestIntention, SceneAssetBundleManifest>(),
                CreateQuery<GetAssetBundleIntention, AssetBundleData>(),
                CreateQuery<GetProfileIntention, Profile>(),
                CreateQuery<GetTextureIntention, Texture2D>(),
                CreateQuery<GetNFTShapeIntention, Texture2D>(),
                CreateQuery<GetEmotesByPointersFromRealmIntention, EmotesDTOList>(),
                CreateQuery<GetOwnedEmotesFromRealmIntention, EmotesResolution>(),
            };
        }

        public GlobalDeferredLoadingSystem(World world, IReleasablePerformanceBudget releasablePerformanceLoadingBudget, IPerformanceBudget memoryBudget)
            : base(world, COMPONENT_HANDLERS, releasablePerformanceLoadingBudget, memoryBudget) { }
    }
}
