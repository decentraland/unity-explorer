using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
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
        /// <summary>
        /// Used to subscribe to backpack events to know when we equip / unequip a smart wearable
        /// </summary>
        private readonly IBackpackEventBus backpackEventBus;

        /// <summary>
        /// Promises waiting on the loading flow of a smart wearable scene
        /// </summary>
        private readonly Dictionary<string, ScenePromise> pendingScenes = new ();

        /// <summary>
        /// IDs of the currently loaded scenes.
        /// </summary>
        private readonly HashSet<string> loadedScenes = new ();

        /// <summary>
        /// IDs of the scenes waiting to be unloaded.
        /// </summary>
        private readonly HashSet<string> toUnload = new ();

        public SmartWearableSystem(World world, IBackpackEventBus backpackEventBus) : base(world)
        {
            this.backpackEventBus = backpackEventBus;
        }

        public override void Initialize()
        {
            base.Initialize();

            backpackEventBus.EquipWearableEvent += OnEquipWearable;
            backpackEventBus.UnEquipWearableEvent += OnUnEquipWearable;
        }

        protected override void Update(float t)
        {
            ResolveScenePromises();
            UnloadSmartWearableScenesQuery(World);
        }

        private void OnEquipWearable(IWearable wearable)
        {
            if (!IsSmartWearable(wearable) || pendingScenes.ContainsKey(wearable.DTO.id) || loadedScenes.Contains(wearable.DTO.id)) return;

            BeginLoadingSmartWearableScene(wearable);
        }

        private void OnUnEquipWearable(IWearable wearable)
        {
            if (!IsSmartWearable(wearable)) return;

            if (pendingScenes.Remove(wearable.DTO.id, out var promise))
            {
                promise.ForgetLoading(World);
                return;
            }

            if (!loadedScenes.Remove(wearable.DTO.id)) return;

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
            ReportHub.Log(GetReportCategory(), $"Equipped Smart Wearable {metadata.id}:{metadata.name}. Loading scene...");

            var partition = PartitionComponent.TOP_PRIORITY;
            var intention = GetSmartWearableSceneIntention.Create(smartWearable, partition);
            var promise = ScenePromise.Create(World, intention, partition);

            pendingScenes.Add(smartWearable.DTO.id, promise);
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
                    return;
                }

                World.Create(
                    new SmartWearableId { Value = id },
                    promise.LoadingIntention.Partition,
                    result.Asset.SceneDefinition,
                    result.Asset.SceneFacade,
                    SceneLoadingState.CreateBuiltScene());

                loadedScenes.Add(id);
            }

            foreach (string id in resolved) pendingScenes.Remove(id);
            resolved.Clear();
        }

        private void UnloadSmartWearableScene(IWearable smartWearable)
        {
            AvatarAttachmentDTO.MetadataBase metadata = smartWearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Unequipped Smart Wearable {metadata.id}:{metadata.name}. Unloading scene...");

            toUnload.Add(smartWearable.DTO.id);
        }

        [Query]
        [All(typeof(SceneDefinitionComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void UnloadSmartWearableScenes(Entity entity, SmartWearableId id)
        {
            if (!toUnload.Remove(id.Value)) return;

            World.Add<DeleteEntityIntention>(entity);
        }
    }
}
