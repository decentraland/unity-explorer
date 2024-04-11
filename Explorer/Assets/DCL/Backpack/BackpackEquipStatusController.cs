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
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEquippedWearables equippedWearables;
        private readonly ISelfProfile selfProfile;

        private World? world;
        private Entity? playerEntity;
        private CancellationTokenSource? publishProfileCts;

        public BackpackEquipStatusController(
            IBackpackEventBus backpackEventBus,
            IEquippedEmotes equippedEmotes,
            IEquippedWearables equippedWearables,
            ISelfProfile selfProfile,
            Func<(World, Entity)> ecsContextProvider
        )
        {
            this.backpackEventBus = backpackEventBus;
            this.equippedEmotes = equippedEmotes;
            this.equippedWearables = equippedWearables;
            this.ecsContextProvider = ecsContextProvider;
            this.selfProfile = selfProfile;
            backpackEventBus.EquipWearableEvent += equippedWearables.Equip;
            backpackEventBus.UnEquipWearableEvent += equippedWearables.UnEquip;
            backpackEventBus.PublishProfileEvent += PublishProfile;
            backpackEventBus.EquipEmoteEvent += equippedEmotes.EquipEmote;
            backpackEventBus.UnEquipEmoteEvent += equippedEmotes.UnEquipEmote;
        }

        public void Dispose()
        {
            backpackEventBus.EquipWearableEvent += equippedWearables.Equip;
            backpackEventBus.UnEquipWearableEvent += equippedWearables.UnEquip;
            backpackEventBus.PublishProfileEvent += PublishProfile;
            backpackEventBus.EquipEmoteEvent += equippedEmotes.EquipEmote;
            backpackEventBus.UnEquipEmoteEvent += equippedEmotes.UnEquipEmote;
            publishProfileCts?.SafeCancelAndDispose();
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

        //This will retrieve the list of default hides for the current equipped wearables

        //Manual hide override will be a separate task

        //TODO retrieve logic from old renderer

        public List<string> GetCurrentWearableHides()
        {
            List<string> hides = new List<string>();

            foreach (string category in WearablesConstants.CATEGORIES_PRIORITY)
            {
                IWearable? wearable = equippedWearables.Wearable(category);

                if (wearable == null)
                    continue;
            }

            return hides;
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
    }
}
