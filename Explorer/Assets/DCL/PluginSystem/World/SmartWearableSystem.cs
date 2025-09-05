using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using System;

namespace DCL.PluginSystem
{
    /// <summary>
    /// Handles initiating loading / unloading scenes tied to smart wearables.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class SmartWearableSystem : BaseUnityLoopSystem
    {
        // Used to subscribe to backpack events to know when we equip / unequip a smart wearable
        private readonly IBackpackEventBus backpackEventBus;

        public SmartWearableSystem(Arch.Core.World world, IBackpackEventBus backpackEventBus) : base(world)
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
        }

        private void OnEquipWearable(IWearable wearable)
        {
            if (!IsSmartWearable(wearable)) return;

            UnloadSmartWearableScene(wearable);
        }

        private void OnUnEquipWearable(IWearable wearable)
        {
            if (!IsSmartWearable(wearable)) return;

            BeginLoadingSmartWearableScene(wearable);
        }

        private bool IsSmartWearable(IWearable wearable)
        {
            // We assume that if the wearable contains a JavaScript asset, then it must be a smart wearable
            // TODO investigate whether there's a better way of identifying smart wearables

            foreach (var content in wearable.DTO.content)
                if (content.file.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private void BeginLoadingSmartWearableScene(IWearable smartWearable)
        {
            AvatarAttachmentDTO.MetadataBase metadata = smartWearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Equipped Smart Wearable {metadata.id}:{metadata.name}. Loading scene...");

            // TODO
        }

        private void UnloadSmartWearableScene(IWearable smartWearable)
        {
            AvatarAttachmentDTO.MetadataBase metadata = smartWearable.DTO.Metadata;
            ReportHub.Log(GetReportCategory(), $"Unequipped Smart Wearable {metadata.id}:{metadata.name}. Unloading scene...");

            // TODO
        }
    }
}
