using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
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
        private readonly IWeb3IdentityCache web3IdentityCache;

        private readonly ICollection<string> forceRender;

        private World? world;
        private Entity? playerEntity;
        private CancellationTokenSource? publishProfileCts;
        private bool hasEquipStatusChanges = false;

        public BackpackEquipStatusController(
            IBackpackEventBus backpackEventBus,
            IEquippedEmotes equippedEmotes,
            IEquippedWearables equippedWearables,
            ISelfProfile selfProfile,
            ICollection<string> forceRender,
            Func<(World, Entity)> ecsContextProvider,
            IWeb3IdentityCache web3IdentityCache
        )
        {
            this.backpackEventBus = backpackEventBus;
            this.equippedEmotes = equippedEmotes;
            this.equippedWearables = equippedWearables;
            this.ecsContextProvider = ecsContextProvider;
            this.web3IdentityCache = web3IdentityCache;
            this.selfProfile = selfProfile;
            this.forceRender = forceRender;

            backpackEventBus.EquipWearableEvent += EquipWearable;
            backpackEventBus.UnEquipWearableEvent += UnEquipWearable;
            backpackEventBus.PublishProfileEvent += PublishProfile;
            backpackEventBus.EquipEmoteEvent += EquipEmote;
            backpackEventBus.UnEquipEmoteEvent += UnEquipEmote;
            backpackEventBus.ForceRenderEvent += SetForceRender;
            backpackEventBus.UnEquipAllEvent += UnEquipAll;
        }

        public void Dispose()
        {
            backpackEventBus.EquipWearableEvent -= EquipWearable;
            backpackEventBus.UnEquipWearableEvent -= UnEquipWearable;
            backpackEventBus.PublishProfileEvent -= PublishProfile;
            backpackEventBus.EquipEmoteEvent -= EquipEmote;
            backpackEventBus.UnEquipEmoteEvent -= UnEquipEmote;
            backpackEventBus.ForceRenderEvent -= SetForceRender;
            backpackEventBus.UnEquipAllEvent -= UnEquipAll;
            publishProfileCts?.SafeCancelAndDispose();
        }

        private void UnEquipAll()
        {
            equippedEmotes.UnEquipAll();
            equippedWearables.UnEquipAll();
            forceRender.Clear();
        }

        private void EquipEmote(int slot, IEmote emote, bool isInitialEquip)
        {
            equippedEmotes.EquipEmote(slot, emote);
            hasEquipStatusChanges = !isInitialEquip;
        }

        private void UnEquipEmote(int slot, IEmote? emote)
        {
            equippedEmotes.UnEquipEmote(slot, emote);
            hasEquipStatusChanges = true;
        }

        private void EquipWearable(IWearable wearable, bool isInitialEquip)
        {
            equippedWearables.Equip(wearable);
            hasEquipStatusChanges = !isInitialEquip;
        }

        private void UnEquipWearable(IWearable wearable)
        {
            equippedWearables.UnEquip(wearable);
            hasEquipStatusChanges = true;
        }

        private void SetForceRender(IReadOnlyCollection<string> categories, bool isInitialHide)
        {
            forceRender.Clear();

            foreach (string category in categories)
                forceRender.Add(category);

            hasEquipStatusChanges = !isInitialHide;
        }

        private void PublishProfile()
        {
            if (!hasEquipStatusChanges || web3IdentityCache.Identity == null)
                return;

            async UniTaskVoid PublishProfileAsync(CancellationToken ct)
            {
                var profile = await selfProfile.PublishAsync(ct);
                // TODO: is it a single responsibility issue? perhaps we can move it elsewhere?
                UpdateAvatarInWorld(profile!);
            }

            hasEquipStatusChanges = false;
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
    }
}
