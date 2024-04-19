using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Backpack
{
    public class BackpackEquipStatusController : IDisposable
    {
        private readonly Func<(World, Entity)> ecsContextProvider;
        private readonly IBackpackCommandBus backpackCommandBus;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEquippedWearables equippedWearables;
        private readonly ISelfProfile selfProfile;

        private readonly ICollection<string> forceRender;

        private World? world;
        private Entity? playerEntity;
        private CancellationTokenSource? publishProfileCts;

        public BackpackEquipStatusController(
            IBackpackEventBus backpackEventBus,
            IEquippedEmotes equippedEmotes,
            IEquippedWearables equippedWearables,
            ISelfProfile selfProfile,
            ICollection<string> forceRender,
            Func<(World, Entity)> ecsContextProvider,
            IBackpackCommandBus backpackCommandBus
        )
        {
            this.backpackEventBus = backpackEventBus;
            this.equippedEmotes = equippedEmotes;
            this.equippedWearables = equippedWearables;
            this.ecsContextProvider = ecsContextProvider;
            this.backpackCommandBus = backpackCommandBus;
            this.selfProfile = selfProfile;
            this.forceRender = forceRender;

            backpackEventBus.EquipWearableEvent += OnWearableEquipped;
            backpackEventBus.UnEquipWearableEvent += equippedWearables.UnEquip;
            backpackEventBus.PublishProfileEvent += PublishProfile;
            backpackEventBus.EquipEmoteEvent += equippedEmotes.EquipEmote;
            backpackEventBus.UnEquipEmoteEvent += equippedEmotes.UnEquipEmote;
            backpackEventBus.ForceRenderEvent += SetForceRender;
        }

        public void Dispose()
        {
            backpackEventBus.EquipWearableEvent -= OnWearableEquipped;
            backpackEventBus.UnEquipWearableEvent -= equippedWearables.UnEquip;
            backpackEventBus.PublishProfileEvent -= PublishProfile;
            backpackEventBus.EquipEmoteEvent -= equippedEmotes.EquipEmote;
            backpackEventBus.UnEquipEmoteEvent -= equippedEmotes.UnEquipEmote;
            backpackEventBus.ForceRenderEvent -= SetForceRender;
            publishProfileCts?.SafeCancelAndDispose();
        }

        private void SetForceRender(IReadOnlyCollection<string> categories)
        {
            forceRender.Clear();

            foreach (string category in categories)
                forceRender.Add(category);
        }

        private void PublishProfile()
        {
            async UniTaskVoid PublishProfileAsync(CancellationToken ct)
            {
                await selfProfile.PublishAsync(ct);
                var profile = await selfProfile.ProfileAsync(ct);

                // TODO: is it a single responsibility issue? perhaps we can move it elsewhere?
                UpdateAvatarInWorld(profile!);
            }

            publishProfileCts = publishProfileCts.SafeRestart();
            PublishProfileAsync(publishProfileCts.Token).Forget();
        }

        private void UpdateAvatarInWorld(Profile profile)
        {
            if (world == null || !playerEntity.HasValue)
            {
                (World? w, Entity e) = ecsContextProvider.Invoke();
                world = w;
                playerEntity = e;
            }

            profile.IsDirty = true;

            bool found = world.Has<Profile>(playerEntity.Value);

            if (found)
                world.Set(playerEntity.Value, profile);
            else
                world.Add(playerEntity.Value, profile);
        }

        private void OnWearableEquipped(IWearable wearable)
        {
            equippedWearables.Equip(wearable);

            if (wearable.GetCategory() == WearablesConstants.Categories.BODY_SHAPE)
                UnEquipIncompatibleWearables(wearable);
        }

        private void UnEquipIncompatibleWearables(IWearable bodyShape)
        {
            foreach ((string? key, IWearable? wearable) in equippedWearables.Items())
            {
                if (wearable == null) continue;
                if (wearable == bodyShape) continue;
                if (wearable.IsCompatibleWithBodyShape(bodyShape.GetUrn())) continue;
                backpackCommandBus.SendCommand(new BackpackUnEquipWearableCommand(key));
            }
        }
    }
}
