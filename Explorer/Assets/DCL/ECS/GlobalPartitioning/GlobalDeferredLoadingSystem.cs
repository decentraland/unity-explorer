using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiles;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using UnityEngine;
using LoadWearableAssetBundleManifestSystem = DCL.AvatarRendering.Wearables.Systems.Load.LoadWearableAssetBundleManifestSystem;
using LoadWearablesByParamSystem = DCL.AvatarRendering.Wearables.Systems.Load.LoadWearablesByParamSystem;
using LoadWearablesDTOByPointersSystem = DCL.AvatarRendering.Wearables.Systems.Load.LoadWearablesDTOByPointersSystem;

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
        private static readonly QueryDescription[] COMPONENT_HANDLERS_SCENES_ASSETS;
        private static readonly QueryDescription[] COMPONENT_HANDLERS_SCENES;

        private Vector2Int teleportParcel;
        private bool downloadOnlySceneMetadata;
        private readonly IScenesCache scenesCache;


        static GlobalDeferredLoadingSystem()
        {
            COMPONENT_HANDLERS_SCENES_ASSETS = new[]
            {
                CreateQuery<GetSceneDefinitionList, SceneDefinitions>(),
                CreateQuery<GetSceneDefinition, SceneEntityDefinition>(),
                CreateQuery<GetSceneFacadeIntention, ISceneFacade>(),
                CreateQuery<GetWearableDTOByPointersIntention, WearablesDTOList>(),
                CreateQuery<GetWearableByParamIntention, IWearable[]>(),
                CreateQuery<GetWearableAssetBundleManifestIntention, SceneAssetBundleManifest>(),
                CreateQuery<GetAssetBundleIntention, AssetBundleData>(),
                CreateQuery<GetTextureIntention, Texture2DData>(),
                CreateQuery<GetEmotesByPointersFromRealmIntention, EmotesDTOList>(),
                CreateQuery<GetOwnedEmotesFromRealmIntention, EmotesResolution>(),
                CreateQuery<GetAudioClipIntention, AudioClipData>(), CreateQuery<GetGLTFIntention, GLTFData>()
            };

            COMPONENT_HANDLERS_SCENES = new[]
            {
                CreateQuery<GetSceneDefinitionList, SceneDefinitions>(), CreateQuery<GetSceneDefinition, SceneEntityDefinition>(), CreateQuery<GetSceneFacadeIntention, ISceneFacade>()
            };
        }

        public GlobalDeferredLoadingSystem(World world, IReleasablePerformanceBudget releasablePerformanceLoadingBudget, IPerformanceBudget memoryBudget, IScenesCache scenesCache)
            : base(world, COMPONENT_HANDLERS_SCENES, releasablePerformanceLoadingBudget, memoryBudget)
        {
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            FilterHandlersIfInTeleport();
            base.Update(t);
        }

        private void FilterHandlersIfInTeleport()
        {
            downloadOnlySceneMetadata = false;
            World.Query(new QueryDescription().WithAll<PlayerTeleportIntent>(), (ref PlayerTeleportIntent teleportIntent) =>
            {
                teleportParcel = teleportIntent.Parcel;
                //If the scene is already in the cache, but not ready, we want to download only its assets.
                if (scenesCache.TryGetByParcel(teleportParcel, out var sceneFacade) && !sceneFacade.IsSceneReady())
                    downloadOnlySceneMetadata = true;
            });

            sameBoatQueries = downloadOnlySceneMetadata ? COMPONENT_HANDLERS_SCENES : COMPONENT_HANDLERS_SCENES_ASSETS;
        }
    }
}
