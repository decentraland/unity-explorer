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
using DCL.Profiles;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Systems;
using PortableExperiences.Controller;
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

        /// <summary>
        /// Promises waiting on the loading flow of a smart wearable scene.
        /// </summary>
        private readonly Dictionary<string, ScenePromise> pendingScenes = new ();

        /// <summary>
        /// IDs of the currently loaded scenes.
        /// </summary>
        private readonly HashSet<string> loadedScenes = new ();

        public SmartWearableSystem(World world, WearableStorage wearableStorage, IBackpackEventBus backpackEventBus, IPortableExperiencesController portableExperiencesController) : base(world)
        {
            this.wearableStorage = wearableStorage;
            this.backpackEventBus = backpackEventBus;
            this.portableExperiencesController = portableExperiencesController;
        }

        public override void Initialize()
        {
            base.Initialize();

            backpackEventBus.EquipWearableEvent += OnEquipWearable;
            backpackEventBus.UnEquipWearableEvent += OnUnEquipWearable;
            portableExperiencesController.PortableExperienceUnloaded += OnPortableExperienceUnloaded;

            HandleInitialEquipmentAsync().Forget();
        }

        protected override void Update(float t)
        {
            ResolveScenePromises();
        }

        private async UniTask HandleInitialEquipmentAsync()
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

        private void OnEquipWearable(IWearable wearable)
        {
            string id = wearable.DTO.Metadata.id;
            if (!IsSmartWearable(wearable) || pendingScenes.ContainsKey(id) || loadedScenes.Contains(id)) return;

            BeginLoadingSmartWearableScene(wearable);
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

        private bool IsSmartWearable(IWearable wearable)
        {
            // We assume that if the wearable contains a scene.json file, then it must be a smart wearable
            // TODO investigate whether there's a better way of identifying smart wearables

            foreach (var content in wearable.DTO.content)
                if (content.file.EndsWith("scene.json", StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
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

        private void ResolveScenePromises()
        {
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

        private void UnloadSmartWearableScene(IWearable smartWearable)
        {
            AvatarAttachmentDTO.MetadataBase metadata = smartWearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Unequipped Smart Wearable '{metadata.name}'. Unloading scene...");

            string ens = smartWearable.DTO.Metadata.id;
            portableExperiencesController.UnloadPortableExperienceById(ens);
        }
    }
}
