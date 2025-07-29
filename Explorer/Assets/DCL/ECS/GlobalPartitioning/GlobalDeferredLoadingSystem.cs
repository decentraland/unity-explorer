using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using DCL.Optimization.PerformanceBudgeting;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using UnityEngine;
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
    [UpdateBefore(typeof(LoadAssetBundleManifestSystem))]
    [UpdateBefore(typeof(LoadGlobalAssetBundleSystem))]
    [UpdateBefore(typeof(LoadWearablesDTOByPointersSystem))]
    [UpdateBefore(typeof(LoadWearablesByParamSystem))]
    public partial class GlobalDeferredLoadingSystem : DeferredLoadingSystem
    {
        private static readonly QueryDescription[] COMPONENT_HANDLERS_SCENES_ASSETS;
        private static readonly QueryDescription[] COMPONENT_HANDLERS_SCENES;

        private readonly IScenesCache scenesCache;
        private readonly Entity playerEntity;

        static GlobalDeferredLoadingSystem()
        {
            COMPONENT_HANDLERS_SCENES_ASSETS = new[]
            {
                CreateQuery<GetSceneDefinitionList, SceneDefinitions>(),
                CreateQuery<GetSceneDefinition, SceneEntityDefinition>(),
                CreateQuery<GetSceneFacadeIntention, ISceneFacade>(),
                CreateQuery<GetWearableDTOByPointersIntention, WearablesDTOList>(),
                CreateQuery<GetWearableByParamIntention, IWearable[]>(),
                CreateQuery<GetAssetBundleManifestIntention, SceneAssetBundleManifest>(),
                CreateQuery<GetAssetBundleIntention, AssetBundleData>(),
                CreateQuery<GetGLTFIntention, GLTFData>(),
                CreateQuery<GetTextureIntention, Texture2DData>(),
                CreateQuery<GetEmotesByPointersFromRealmIntention, EmotesDTOList>(),
                CreateQuery<GetOwnedEmotesFromRealmIntention, EmotesResolution>(),
                CreateQuery<GetAudioClipIntention, AudioClipData>(),
                CreateQuery<GetGLTFIntention, GLTFData>()
            };

            COMPONENT_HANDLERS_SCENES = new[]
            {
                CreateQuery<GetSceneDefinitionList, SceneDefinitions>(),
                CreateQuery<GetSceneDefinition, SceneEntityDefinition>()
            };
        }

        public GlobalDeferredLoadingSystem(World world, IReleasablePerformanceBudget releasablePerformanceLoadingBudget, IPerformanceBudget memoryBudget, IScenesCache scenesCache, Entity playerEntity)
            : base(world, COMPONENT_HANDLERS_SCENES, releasablePerformanceLoadingBudget, memoryBudget)
        {
            this.scenesCache = scenesCache;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            FilterHandlersIfInTeleport();
            base.Update(t);
        }

        private void FilterHandlersIfInTeleport()
        {
            bool downloadOnlySceneMetadata = false;
            //We check if the player is teleporting, and if the scene we want to teleport to has started.
            //If so, only scene metadata will be allowed to de downloaded
            TeleportUtils.PlayerTeleportingState teleportParcel = TeleportUtils.GetTeleportParcel(World, playerEntity);
            if (teleportParcel.IsTeleporting)
            {
                if (scenesCache.Contains(teleportParcel.Parcel))
                    downloadOnlySceneMetadata = true;
            }

            sameBoatQueries = downloadOnlySceneMetadata ? COMPONENT_HANDLERS_SCENES : COMPONENT_HANDLERS_SCENES_ASSETS;
        }
    }
}
