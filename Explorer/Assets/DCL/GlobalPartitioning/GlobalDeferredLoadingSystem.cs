using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Ipfs;
using SceneRunner.Scene;

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
    [UpdateBefore(typeof(LoadWearablesByPointersSystem))]
    [UpdateBefore(typeof(LoadWearablesByParamSystem))]
    public partial class GlobalDeferredLoadingSystem : DeferredLoadingSystem
    {
        private static readonly QueryDescription[] COMPONENT_HANDLERS;

        static GlobalDeferredLoadingSystem()
        {
            COMPONENT_HANDLERS = new[]
            {
                CreateQuery<GetSceneDefinitionList, SceneDefinitions>(),
                CreateQuery<GetSceneDefinition, IpfsTypes.SceneEntityDefinition>(),
                CreateQuery<GetSceneFacadeIntention, ISceneFacade>(),
                CreateQuery<GetWearableDTOByPointersIntention, WearableDTO[]>(),
                CreateQuery<GetWearableyParamIntention, Wearable[]>(),
                CreateQuery<GetWearableAssetBundleManifestIntention, SceneAssetBundleManifest>(),
                CreateQuery<GetAssetBundleIntention, AssetBundleData>(),
            };
        }

        internal GlobalDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider)
            : base(world, COMPONENT_HANDLERS, concurrentLoadingBudgetProvider) { }
    }
}
