using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace DCL.Backpack
{
    public class BackpackEquipStatusController : IDisposable
    {
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEquippedWearables equippedWearables;
        private readonly ISelfProfile selfProfile;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ICollection<string> forceRender;
        private readonly IAppArgs appArgs;
        private readonly World world;
        private readonly Entity playerEntity;
        private CancellationTokenSource? publishProfileCts;

        public BackpackEquipStatusController(
            IBackpackEventBus backpackEventBus,
            IEquippedEmotes equippedEmotes,
            IEquippedWearables equippedWearables,
            ISelfProfile selfProfile,
            ICollection<string> forceRender,
            IWeb3IdentityCache web3IdentityCache,
            World world,
            Entity playerEntity,
            IAppArgs appArgs)
        {
            this.backpackEventBus = backpackEventBus;
            this.equippedEmotes = equippedEmotes;
            this.equippedWearables = equippedWearables;
            this.web3IdentityCache = web3IdentityCache;
            this.selfProfile = selfProfile;
            this.forceRender = forceRender;

            backpackEventBus.EquipWearableEvent += EquipWearable;
            backpackEventBus.UnEquipWearableEvent += UnEquipWearable;
            backpackEventBus.PublishProfileEvent += UpdateProfile;
            backpackEventBus.EquipEmoteEvent += EquipEmote;
            backpackEventBus.UnEquipEmoteEvent += UnEquipEmote;
            backpackEventBus.ChangeColorEvent += ChangeColor;
            backpackEventBus.ForceRenderEvent += SetForceRender;
            backpackEventBus.UnEquipAllEvent += UnEquipAll;

            this.world = world;
            this.playerEntity = playerEntity;
            this.appArgs = appArgs;
        }

        public void Dispose()
        {
            backpackEventBus.EquipWearableEvent -= EquipWearable;
            backpackEventBus.UnEquipWearableEvent -= UnEquipWearable;
            backpackEventBus.PublishProfileEvent -= UpdateProfile;
            backpackEventBus.EquipEmoteEvent -= EquipEmote;
            backpackEventBus.UnEquipEmoteEvent -= UnEquipEmote;
            backpackEventBus.ChangeColorEvent -= ChangeColor;
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

        private void EquipEmote(int slot, IEmote emote, bool _)
        {
            equippedEmotes.EquipEmote(slot, emote);
        }

        private void UnEquipEmote(int slot, IEmote? emote)
        {
            equippedEmotes.UnEquipEmote(slot, emote);
        }

        private void EquipWearable(IWearable wearable)
        {
            equippedWearables.Equip(wearable);
        }

        private void UnEquipWearable(IWearable wearable)
        {
            equippedWearables.UnEquip(wearable);
        }

        private void SetForceRender(IReadOnlyCollection<string> categories)
        {
            forceRender.Clear();

            foreach (string category in categories)
                forceRender.Add(category);
        }

        private void ChangeColor(Color newColor, string category)
        {
            switch (category)
            {
                case WearablesConstants.Categories.EYES:
                    equippedWearables.SetEyesColor(newColor);
                    break;
                case WearablesConstants.Categories.HAIR:
                    equippedWearables.SetHairColor(newColor);
                    break;
                case WearablesConstants.Categories.BODY_SHAPE:
                    equippedWearables.SetBodyshapeColor(newColor);
                    break;
            }
        }

        private void UpdateProfile()
        {
            if (web3IdentityCache.Identity == null)
                return;

            publishProfileCts = publishProfileCts.SafeRestart();
            UpdateProfileAsync(publishProfileCts.Token).Forget();
        }

        private async UniTaskVoid UpdateProfileAsync(CancellationToken ct)
        {
            bool publishProfileChange = !appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTION)
                                        && !appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_WEARABLES);

            var profile = await selfProfile.UpdateProfileAsync(publish: publishProfileChange, ct);
            MultithreadingUtility.AssertMainThread(nameof(UpdateProfileAsync), true);
            UpdateAvatarInWorld(profile!);
        }

        private void UpdateAvatarInWorld(Profile profile)
        {
            profile.IsDirty = true;

            bool found = world.Has<Profile>(playerEntity);

            if (found)
                world.Set(playerEntity, profile);
            else
                world.Add(playerEntity, profile);
        }
    }
}
