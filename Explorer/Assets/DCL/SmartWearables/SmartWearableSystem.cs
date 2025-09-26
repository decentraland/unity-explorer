using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Profiles;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Systems;
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
        private readonly IBackpackSharedAPI backpackSharedAPI;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly IScenesCache scenesCache;

        /// <summary>
        /// Promises waiting on the loading flow of a smart wearable scene.
        /// </summary>
        private readonly Dictionary<string, ScenePromise> pendingScenes = new ();

        /// <summary>
        /// IDs of the currently loaded scenes.
        /// </summary>
        private readonly HashSet<string> runningScenes = new ();

        public SmartWearableSystem(World world, WearableStorage wearableStorage, SmartWearableCache smartWearableCache, IBackpackSharedAPI backpackSharedAPI, IPortableExperiencesController portableExperiencesController, IScenesCache scenesCache) : base(world)
        {
            this.wearableStorage = wearableStorage;
            this.smartWearableCache = smartWearableCache;
            this.backpackSharedAPI = backpackSharedAPI;
            this.portableExperiencesController = portableExperiencesController;
            this.scenesCache = scenesCache;
        }

        public override void Initialize()
        {
            base.Initialize();

            backpackSharedAPI.WearableEquipped += OnWearableEquipped;
            backpackSharedAPI.WearableUnEquipped += OnWearableUnEquipped;
            portableExperiencesController.PortableExperienceUnloaded += OnPortableExperienceUnloaded;
            scenesCache.CurrentScene.OnUpdate += OnCurrentSceneChanged;

            RunScenesForEquippedWearablesAsync().Forget();
        }

        private void OnWearableEquipped(string urn, bool isManuallyEquipped)
        {
            if (!isManuallyEquipped) return;

            wearableStorage.TryGetElement(urn, out IWearable wearable);
            TryRunSmartWearableSceneAsync(wearable).Forget();
        }

        private async UniTask TryRunSmartWearableSceneAsync(IWearable wearable)
        {
            bool isSmart = await smartWearableCache.IsSmartAsync(wearable, CancellationToken.None);
            if (!isSmart || !SceneAllowsSmartWearables()) return;

            string id = wearable.DTO.Metadata.id;
            if (pendingScenes.ContainsKey(id) || runningScenes.Contains(id)) return;

            AvatarAttachmentDTO.MetadataBase metadata = wearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Equipped Smart Wearable '{metadata.name}'. Loading scene...");

            var partition = PartitionComponent.TOP_PRIORITY;
            var intention = GetSmartWearableSceneIntention.Create(wearable, partition);
            var promise = ScenePromise.Create(World, intention, partition);

            pendingScenes.Add(metadata.id, promise);
        }

        private void OnWearableUnEquipped(string urn)
        {
            wearableStorage.TryGetElement(urn, out IWearable wearable);

            StopSmartWearableSceneAsync(wearable).Forget();
        }

        private async UniTask StopSmartWearableSceneAsync(IWearable wearable)
        {
            bool isSmart = await smartWearableCache.IsSmartAsync(wearable, CancellationToken.None);
            if (!isSmart) return;

            string id = wearable.DTO.Metadata.id;

            if (pendingScenes.Remove(id, out var promise))
            {
                promise.ForgetLoading(World);
                return;
            }

            if (!runningScenes.Remove(id)) return;

            AvatarAttachmentDTO.MetadataBase metadata = wearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Unequipped Smart Wearable '{metadata.name}'. Unloading scene...");

            string ens = wearable.DTO.Metadata.id;
            portableExperiencesController.UnloadPortableExperienceById(ens);
        }

        protected override void Update(float t)
        {
            ResolveScenePromises();
        }

        private void ResolveScenePromises()
        {
            if (!SceneAllowsSmartWearables())
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

                runningScenes.Add(id);
            }

            foreach (string id in resolved) pendingScenes.Remove(id);
        }

        private void AddPortableExperience(IWearable smartWearable, Entity scene)
        {
            string id = smartWearable.DTO.Metadata.id;

            var metadata = new PortableExperienceMetadata
            {
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
            runningScenes.Remove(id);
        }

        private void OnCurrentSceneChanged(ISceneFacade scene)
        {
            bool allowsSmartWearables = SceneAllowsSmartWearables();

            ReportHub.Log(GetReportCategory(), "Current Scene allows Smart Wearables: " + allowsSmartWearables);

            if (allowsSmartWearables)
                // Notice scenes that are already running won't run again, so we can call this safely.
                RunScenesForEquippedWearablesAsync().Forget();
            else
            {
                foreach ((string _, ScenePromise promise) in pendingScenes) promise.ForgetLoading(World);
                pendingScenes.Clear();

                if (runningScenes.Count > 0)
                {
                    // The reason for the copy is that unloading a PX will also trigger an event that alters the loaded scenes set
                    using var tempScope = ListPool<string>.Get(out var temp);
                    temp.AddRange(runningScenes);
                    foreach (string id in temp) portableExperiencesController.UnloadPortableExperienceById(id);
                }
            }
        }

        private bool SceneAllowsSmartWearables()
        {
            var scene = scenesCache.CurrentScene.Value;

            // If we aren't in a scene we just allow Smart Wearables.
            if (scene == null) return true;

            // Otherwise we check the feature toggles.
            SceneMetadata.FeatureToggles featureToggles = scene.SceneData.SceneEntityDefinition.metadata.featureToggles;
            return featureToggles.PortableExperiencesEnabled;
        }

        private async UniTask RunScenesForEquippedWearablesAsync()
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

                await TryRunSmartWearableSceneAsync(wearable);
            }
        }
    }
}
