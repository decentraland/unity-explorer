using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
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
        private readonly IProfileCache profileCache;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly List<string> forceRender;
        private readonly IEmoteStorage emoteStorage;
        private readonly IWearableStorage wearableStorage;
        private readonly IAppArgs appArgs;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly ProfileBuilder profileBuilder = new ();
        private readonly bool publishProfileChanges;
        private CancellationTokenSource? publishProfileCts;

        public BackpackEquipStatusController(
            IBackpackEventBus backpackEventBus,
            IEquippedEmotes equippedEmotes,
            IEquippedWearables equippedWearables,
            ISelfProfile selfProfile,
            IProfileCache profileCache,
            List<string> forceRender,
            IEmoteStorage emoteStorage,
            IWearableStorage wearableStorage,
            IWeb3IdentityCache web3IdentityCache,
            World world,
            Entity playerEntity,
            IAppArgs appArgs,
            WarningNotificationView inWorldWarningNotificationView)
        {
            this.backpackEventBus = backpackEventBus;
            this.equippedEmotes = equippedEmotes;
            this.equippedWearables = equippedWearables;
            this.web3IdentityCache = web3IdentityCache;
            this.selfProfile = selfProfile;
            this.profileCache = profileCache;
            this.forceRender = forceRender;
            this.emoteStorage = emoteStorage;
            this.wearableStorage = wearableStorage;

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
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;

            publishProfileChanges = !appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS)
                                    && !appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_WEARABLES);
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
            Profile? oldProfile = await selfProfile.ProfileAsync(ct);

            if (oldProfile == null)
            {
                ShowErrorNotificationAsync(ct).Forget();
                return;
            }

            Profile newProfile = oldProfile.CreateNewProfileForUpdate(equippedEmotes, equippedWearables,
                forceRender, emoteStorage, wearableStorage,
                // Don't increment the version as it will be incremented later on selfProfile.UpdateProfileAsync
                !publishProfileChanges);

            // Skip publishing the same profile
            if (newProfile.Avatar.IsSameAvatar(oldProfile.Avatar))
                return;

            // Clone the old profile since the original will be disposed when its replaced in the cache
            oldProfile = profileBuilder.From(oldProfile).Build();

            // Update profile immediately to prevent UI inconsistencies
            // Without this immediate update, temporary desync can occur between backpack closure and catalyst validation
            // Example: Opening the emote wheel before catalyst validation would show outdated emote selections
            profileCache.Set(newProfile.UserId, newProfile);
            UpdateAvatarInWorld(newProfile);

            if (!publishProfileChanges) return;

            try
            {
                Profile? savedProfile = await selfProfile.UpdateProfileAsync(newProfile, ct);
                MultithreadingUtility.AssertMainThread(nameof(UpdateProfileAsync), true);
                // We need to re-update the avatar in-world with the new profile because the save operation invalidates the previous profile
                // breaking the avatar and the backpack
                UpdateAvatarInWorld(savedProfile!);
                oldProfile.Dispose();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.PROFILE);

                // Revert to the old profile so we are aligned to the catalyst's version
                profileCache.Set(oldProfile.UserId, oldProfile);
                UpdateAvatarInWorld(oldProfile);

                ShowErrorNotificationAsync(ct).Forget();
            }
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

        private async UniTask ShowErrorNotificationAsync(CancellationToken ct)
        {
            inWorldWarningNotificationView.SetText("There was an error updating your avatar profile. Please try again.");
            inWorldWarningNotificationView.Show(ct);

            await UniTask.Delay(3000, cancellationToken: ct);

            inWorldWarningNotificationView.Hide(ct: ct);
        }
    }
}
