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
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
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
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly IScenesCache scenesCache;

        /// <summary>
        /// Promises waiting on the loading flow of a smart wearable scene.
        /// </summary>
        private readonly Dictionary<string, ScenePromise> pendingScenes = new ();

        /// <summary>
        /// IDs of the currently loaded scenes.
        /// </summary>
        private readonly HashSet<string> loadedScenes = new ();

        public SmartWearableSystem(World world, WearableStorage wearableStorage, IBackpackEventBus backpackEventBus, IPortableExperiencesController portableExperiencesController, IScenesCache scenesCache) : base(world)
        {
            this.wearableStorage = wearableStorage;
            this.backpackEventBus = backpackEventBus;
            this.portableExperiencesController = portableExperiencesController;
            this.scenesCache = scenesCache;
        }

        public override void Initialize()
        {
            base.Initialize();

            backpackEventBus.EquipWearableEvent += OnEquipWearable;
            backpackEventBus.UnEquipWearableEvent += OnUnEquipWearable;
            portableExperiencesController.PortableExperienceUnloaded += OnPortableExperienceUnloaded;
            scenesCache.CurrentScene.OnUpdate += OnCurrentSceneChanged;

            RunScenesForEquippedWearablesAsync().Forget();
        }

        private void OnEquipWearable(IWearable wearable)
        {
            string id = wearable.DTO.Metadata.id;

            if (!IsSmartWearable(wearable) || !SceneAllowsSmartWearables() || pendingScenes.ContainsKey(id) || loadedScenes.Contains(id)) return;

            BeginLoadingSmartWearableScene(wearable);
        }

        private void BeginLoadingSmartWearableScene(IWearable smartWearable)
        {
            AvatarAttachmentDTO.MetadataBase metadata = smartWearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Equipped Smart Wearable '{metadata.name}'. Loading scene...");

            var partition = PartitionComponent.TOP_PRIORITY;
            var intention = GetSmartWearableSceneIntention.Create(smartWearable, partition);
            var promise = ScenePromise.Create(World, intention, partition);

            pendingScenes.Add(metadata.id, promise);
        }

        private void OnUnEquipWearable(IWearable wearable)
        {
            if (!IsSmartWearable(wearable)) return;

            string id = wearable.DTO.Metadata.id;

            if (pendingScenes.Remove(id, out var promise))
            {
                promise.ForgetLoading(World);
                return;
            }

            if (!loadedScenes.Remove(id)) return;

            UnloadSmartWearableScene(wearable);
        }

        private void UnloadSmartWearableScene(IWearable smartWearable)
        {
            AvatarAttachmentDTO.MetadataBase metadata = smartWearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Unequipped Smart Wearable '{metadata.name}'. Unloading scene...");

            string ens = smartWearable.DTO.Metadata.id;
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

                loadedScenes.Add(id);
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
            loadedScenes.Remove(id);
        }

        private void OnCurrentSceneChanged(ISceneFacade scene)
        {
            if (SceneAllowsSmartWearables())
                // Notice scenes that are already running won't run again, so we can call this safely.
                RunScenesForEquippedWearablesAsync().Forget();
            else
            {
                foreach ((string _, ScenePromise promise) in pendingScenes) promise.ForgetLoading(World);
                pendingScenes.Clear();

                if (loadedScenes.Count > 0)
                {
                    // The reason for the copy is that unloading a PX will also trigger an event that alters the loaded scenes set
                    using var tempScope = ListPool<string>.Get(out var temp);
                    temp.AddRange(loadedScenes);
                    foreach (string id in temp) portableExperiencesController.UnloadPortableExperienceById(id);
                }
            }
        }

        private bool IsSmartWearable(IWearable wearable)
        {
            // We assume that if the wearable contains a scene.json file, then it must be a smart wearable

            foreach (var content in wearable.DTO.content)
                if (content.file.EndsWith("scene.json", StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
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

                OnEquipWearable(wearable);
            }
        }
    }
}
