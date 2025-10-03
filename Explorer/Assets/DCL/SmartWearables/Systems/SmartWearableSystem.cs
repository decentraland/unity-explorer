using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Profiles;
using DCL.RealmNavigation;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Systems;
using MVC;
using PortableExperiences.Controller;
using Runtime.Wearables;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using ScenePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.SceneLifeCycle.Systems.GetSmartWearableSceneIntention.Result, ECS.SceneLifeCycle.Systems.GetSmartWearableSceneIntention>;

namespace DCL.SmartWearables
{
    /// <summary>
    /// Handles initiating loading / unloading scenes tied to smart wearables.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class SmartWearableSystem : BaseUnityLoopSystem
    {
        private readonly WearableStorage wearableStorage;
        private readonly SmartWearableCache smartWearableCache;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly IScenesCache scenesCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly IMVCManager mvcManager;
        private readonly IThumbnailProvider thumbnailProvider;

        /// <summary>
        ///     Promises waiting on the loading flow of a smart wearable scene.
        /// </summary>
        private readonly Dictionary<string, ScenePromise> pendingScenes = new ();

        private bool currentSceneDirty;

        public SmartWearableSystem(World world,
            WearableStorage wearableStorage,
            SmartWearableCache smartWearableCache,
            IBackpackEventBus backpackEventBus,
            IPortableExperiencesController portableExperiencesController,
            IScenesCache scenesCache,
            ILoadingStatus loadingStatus,
            IMVCManager mvcManager,
            IThumbnailProvider thumbnailProvider) : base(world)
        {
            this.wearableStorage = wearableStorage;
            this.smartWearableCache = smartWearableCache;
            this.backpackEventBus = backpackEventBus;
            this.portableExperiencesController = portableExperiencesController;
            this.scenesCache = scenesCache;
            this.loadingStatus = loadingStatus;
            this.mvcManager = mvcManager;
            this.thumbnailProvider = thumbnailProvider;
        }

        public override void Initialize()
        {
            base.Initialize();

            backpackEventBus.EquipWearableEvent += OnEquipWearable;
            backpackEventBus.UnEquipWearableEvent += OnUnEquipWearable;
            portableExperiencesController.PortableExperienceUnloaded += OnPortableExperienceUnloaded;
            scenesCache.CurrentScene.OnUpdate += OnCurrentSceneChanged;

            loadingStatus.CurrentStage.OnUpdate += OnLoadingStatusChanged;
        }

        private void OnEquipWearable(IWearable wearable, bool isManuallyEquipped)
        {
            if (!isManuallyEquipped) return;

            TryRunSmartWearableSceneAsync(wearable).Forget();
        }

        private async UniTask TryRunSmartWearableSceneAsync(IWearable wearable)
        {
            bool isSmart = await smartWearableCache.IsSmartAsync(wearable, CancellationToken.None);
            if (!isSmart || !smartWearableCache.CurrentSceneAllowsSmartWearables) return;

            string id = SmartWearableCache.GetCacheId(wearable);
            if (pendingScenes.ContainsKey(id) ||
                smartWearableCache.RunningSmartWearables.Contains(id) ||
                // Do not load scenes that were manually killed
                // To re-enable a wearable, the user must unequip it and then equip it again
                // NOTICE reloading can be triggered whenever moving between scenes too, that's why we need this
                smartWearableCache.KilledPortableExperiences.Contains(id)) return;

            AvatarAttachmentDTO.MetadataBase metadata = wearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Equipped Smart Wearable '{metadata.name}'. Loading scene...");

            var partition = PartitionComponent.TOP_PRIORITY;
            var intention = GetSmartWearableSceneIntention.Create(wearable, partition);
            var promise = ScenePromise.Create(World, intention, partition);

            pendingScenes.Add(metadata.id, promise);
        }

        private void OnUnEquipWearable(IWearable wearable)
        {
            StopSmartWearableSceneAsync(wearable).Forget();
        }

        private async UniTask StopSmartWearableSceneAsync(IWearable wearable)
        {
            bool isSmart = await smartWearableCache.IsSmartAsync(wearable, CancellationToken.None);
            if (!isSmart) return;

            string id = SmartWearableCache.GetCacheId(wearable);

            // If the user removes the wearable, we can allow reloading its scene the next time it is equipped
            smartWearableCache.KilledPortableExperiences.Remove(id);

            if (pendingScenes.Remove(id, out var promise))
            {
                promise.ForgetLoading(World);
                return;
            }

            if (!smartWearableCache.RunningSmartWearables.Remove(id)) return;

            AvatarAttachmentDTO.MetadataBase metadata = wearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Unequipped Smart Wearable '{metadata.name}'. Unloading scene...");

            portableExperiencesController.UnloadPortableExperienceById(id);
        }

        protected override void Update(float t)
        {
            smartWearableCache.CurrentSceneAllowsSmartWearables = CurrentSceneAllowsSmartWearables();

            ResolveScenePromises();

            if (currentSceneDirty)
            {
                HandleSceneChange();
                currentSceneDirty = false;
            }
        }

        private bool CurrentSceneAllowsSmartWearables()
        {
            var scene = scenesCache.CurrentScene.Value;

            // If we aren't in a scene we just allow Smart Wearables.
            if (scene == null) return true;

            // Otherwise we check the feature toggles.
            SceneMetadata.FeatureToggles featureToggles = scene.SceneData.SceneEntityDefinition.metadata.featureToggles;
            return featureToggles.PortableExperiencesEnabled;
        }

        private void ResolveScenePromises()
        {
            if (!smartWearableCache.CurrentSceneAllowsSmartWearables)
            {
                foreach ((string _, ScenePromise promise) in pendingScenes) promise.ForgetLoading(World);
                pendingScenes.Clear();
                return;
            }

            using var resolvedScope = HashSetPool<string>.Get(out var resolved);

            foreach ((string id, ScenePromise promise) in pendingScenes)
            {
                if (!promise.TryConsume(World, out var result)) continue;

                resolved.Add(id);

                if (!result.Succeeded)
                {
                    if (result.Exception != null) ReportHub.LogError(GetReportCategory(), result.Exception);
                    continue;
                }

                Entity scene = World.Create(
                    new SmartWearableId { Value = id },
                    promise.LoadingIntention.Partition,
                    result.Asset.SceneDefinition,
                    result.Asset.SceneFacade,
                    SceneLoadingState.CreateBuiltScene());

                AddPortableExperience(promise.LoadingIntention.SmartWearable, scene);

                smartWearableCache.RunningSmartWearables.Add(id);
            }

            foreach (string id in resolved) pendingScenes.Remove(id);
        }

        private void AddPortableExperience(IWearable smartWearable, Entity scene)
        {
            string id = SmartWearableCache.GetCacheId(smartWearable);

            var metadata = new PortableExperienceMetadata
            {
                Type = PortableExperienceType.SMART_WEARABLE,
                Ens = string.Empty,
                Id = id,
                Name = smartWearable.DTO.Metadata.name,
                ParentSceneId = "avatar"
            };
            World.Add(scene, metadata);

            portableExperiencesController.AddPortableExperience(id, scene);
        }


        private void OnPortableExperienceUnloaded(string id)
        {
            smartWearableCache.RunningSmartWearables.Remove(id);
        }

        private void OnCurrentSceneChanged(ISceneFacade scene)
        {
            currentSceneDirty = true;
        }

        private void HandleSceneChange()
        {
            bool smartWearablesAllowed = smartWearableCache.CurrentSceneAllowsSmartWearables;

            ReportHub.Log(GetReportCategory(), "Current Scene allows Smart Wearables: " + smartWearablesAllowed);

            if (smartWearablesAllowed)
                // Notice scenes that are already running won't run again, so we can call this safely
                // TODO consider cancelling a previous running task
                RunScenesForEquippedWearablesAsync(AuthorizationAction.SkipAuthorization, CancellationToken.None).Forget();
            else
            {
                foreach ((string _, ScenePromise promise) in pendingScenes) promise.ForgetLoading(World);
                pendingScenes.Clear();

                if (smartWearableCache.RunningSmartWearables.Count > 0)
                {
                    // The reason for the copy is that unloading a PX will also trigger an event that alters the loaded scenes set
                    using var tempScope = ListPool<string>.Get(out var temp);
                    temp.AddRange(smartWearableCache.RunningSmartWearables);
                    foreach (string id in temp) portableExperiencesController.UnloadPortableExperienceById(id);
                }
            }
        }

        private void OnLoadingStatusChanged(LoadingStatus.LoadingStage stage)
        {
            if (stage != LoadingStatus.LoadingStage.Completed) return;

            RunScenesForEquippedWearablesAsync(AuthorizationAction.RequestAuthorization, CancellationToken.None).Forget();
        }

        private async UniTask RunScenesForEquippedWearablesAsync(AuthorizationAction authorization, CancellationToken ct)
        {
            Entity player = Entity.Null;
            while (player == Entity.Null)
            {
                player = World.CachePlayer();
                await UniTask.Yield();
            }

            Profile profile;
            while (!World.TryGet(player, out profile)) await UniTask.Yield();

            foreach (var urn in profile.Avatar.Wearables)
            {
                IWearable wearable;

                URN shortUrn = urn.Shorten();
                while (!wearableStorage.TryGetElement(shortUrn, out wearable) || wearable.IsLoading) await UniTask.Yield();

                string id = SmartWearableCache.GetCacheId(wearable);

                // By design at this point of the flow we only request auth if the wearable uses the Web3 API
                // When equipping from the backpack, we request auth for any required permission
                bool requiresAuthorization = authorization == AuthorizationAction.RequestAuthorization &&
                                             await smartWearableCache.RequiresWeb3APIAsync(wearable, CancellationToken.None);

                if (requiresAuthorization && !smartWearableCache.AuthorizedSmartWearables.Contains(id))
                {
                    // Make sure the thumbnail is there
                    // Needed because we also run this flow on login, and thumbnails are loaded on-demand
                    await thumbnailProvider.GetAsync(wearable, ct);

                    bool authorized = await SmartWearableAuthorizationPopupController.RequestAuthorizationAsync(mvcManager, wearable, ct);
                    if (authorized)
                        smartWearableCache.AuthorizedSmartWearables.Add(id);
                    else
                    {
                        smartWearableCache.KilledPortableExperiences.Add(id);
                        continue;
                    }
                }

                await TryRunSmartWearableSceneAsync(wearable);
            }
        }

        private enum AuthorizationAction
        {
            RequestAuthorization,

            SkipAuthorization
        }
    }
}
