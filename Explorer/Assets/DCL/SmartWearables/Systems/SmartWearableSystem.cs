using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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

            string wearableName = wearable.DTO.Metadata.name;
            ReportHub.Log(GetReportCategory(), $"Equipped Smart Wearable '{wearableName}'. Loading scene...");

            var partition = PartitionComponent.TOP_PRIORITY;
            var intention = GetSmartWearableSceneIntention.Create(wearable, partition);

            await UniTask.SwitchToMainThread();

            var promise = ScenePromise.Create(World, intention, partition);
            World.Add(promise.Entity, promise, new SmartWearableId { Value = id });

            pendingScenes.Add(SmartWearableCache.GetCacheId(wearable), promise);
        }

        private void OnUnEquipWearable(IWearable wearable) =>
            StopSmartWearableSceneAsync(wearable).Forget();

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

            string wearableName = wearable.DTO.Metadata.name;
            ReportHub.Log(GetReportCategory(), $"Unequipped Smart Wearable '{wearableName}'. Unloading scene...");

            portableExperiencesController.UnloadPortableExperienceById(id);
        }

        protected override void Update(float t)
        {
            smartWearableCache.CurrentSceneAllowsSmartWearables = CurrentSceneAllowsSmartWearables();

            if (smartWearableCache.CurrentSceneAllowsSmartWearables) ResolveScenePromiseQuery(World);

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

        [Query]
        private void ResolveScenePromise(ref ScenePromise promise, in SmartWearableId smartWearableId)
        {
            if (!promise.TryConsume(World, out var result)) return;

            if (!result.Succeeded)
            {
                if (result.Exception != null) ReportHub.LogError(GetReportCategory(), result.Exception);
                return;
            }

            Entity scene = World.Create(
                smartWearableId,
                promise.LoadingIntention.Partition,
                result.Asset.SceneDefinition,
                result.Asset.SceneFacade,
                SceneLoadingState.CreateBuiltScene());

            AddPortableExperience(promise.LoadingIntention.SmartWearable, scene);

            smartWearableCache.RunningSmartWearables.Add(smartWearableId.Value);
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

        private void CancelLoadingAllScenes()
        {
            CancelLoadingSceneQuery(World);
            pendingScenes.Clear();
        }

        [Query]
        [All(typeof(SmartWearableId))]
        private void CancelLoadingScene(ref ScenePromise promise) =>
            promise.ForgetLoading(World);

        private void OnPortableExperienceUnloaded(string id) =>
            smartWearableCache.RunningSmartWearables.Remove(id);

        private void OnCurrentSceneChanged(ISceneFacade scene) =>
            currentSceneDirty = true;

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
                CancelLoadingAllScenes();

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

            // Do once, then listen to scene changes
            loadingStatus.CurrentStage.OnUpdate -= OnLoadingStatusChanged;
            scenesCache.CurrentScene.OnUpdate += OnCurrentSceneChanged;

            RunScenesForEquippedWearablesAsync(AuthorizationAction.RequestAuthorization, CancellationToken.None).Forget();
        }

        private async UniTask RunScenesForEquippedWearablesAsync(AuthorizationAction authorization, CancellationToken ct)
        {
            Entity player = World.CachePlayer();
            Profile profile = World.Get<Profile>(player);

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
