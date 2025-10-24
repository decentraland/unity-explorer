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
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DCL.AvatarRendering.Loading.Components;
using ECS;
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
        private readonly IEmoteStorage emoteStorage;
        private readonly IWearableStorage wearableStorage;
        private readonly IAppArgs appArgs;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly World world;
        private readonly IRealmData realmData;
        private readonly Entity playerEntity;
        private CancellationTokenSource? publishProfileCts;

        public BackpackEquipStatusController(
            IBackpackEventBus backpackEventBus,
            IEquippedEmotes equippedEmotes,
            IEquippedWearables equippedWearables,
            ISelfProfile selfProfile,
            IProfileCache profileCache,
            IEmoteStorage emoteStorage,
            IWearableStorage wearableStorage,
            IWeb3IdentityCache web3IdentityCache,
            World world,
            Entity playerEntity,
            IAppArgs appArgs,
            WarningNotificationView inWorldWarningNotificationView,
            ProfileChangesBus profileChangesBus,
            IRealmData realmData)
        {
            this.backpackEventBus = backpackEventBus;
            this.equippedEmotes = equippedEmotes;
            this.equippedWearables = equippedWearables;
            this.web3IdentityCache = web3IdentityCache;
            this.selfProfile = selfProfile;
            this.profileCache = profileCache;
            this.emoteStorage = emoteStorage;
            this.wearableStorage = wearableStorage;
            this.realmData = realmData;

            backpackEventBus.EquipWearableEvent += EquipWearable;
            backpackEventBus.UnEquipWearableEvent += UnEquipWearable;
            backpackEventBus.PublishProfileEvent += UpdateProfile;
            backpackEventBus.EquipEmoteEvent += EquipEmote;
            backpackEventBus.UnEquipEmoteEvent += UnEquipEmote;
            backpackEventBus.ChangeColorEvent += ChangeColor;
            backpackEventBus.ForceRenderEvent += SetForceRender;
            backpackEventBus.UnEquipAllEvent += UnEquipAll;
            backpackEventBus.UnEquipAllWearablesEvent += UnEquipAllWearables;
            // Avoid publishing an invalid profile
            // For example: logout while the update operation is being processed
            // See: https://github.com/decentraland/unity-explorer/issues/4413
            web3IdentityCache.OnIdentityCleared += CancelUpdateOperation;
            web3IdentityCache.OnIdentityChanged += CancelUpdateOperation;

            this.world = world;
            this.playerEntity = playerEntity;
            this.appArgs = appArgs;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.profileChangesBus = profileChangesBus;
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
            backpackEventBus.UnEquipAllWearablesEvent -= UnEquipAllWearables;
            web3IdentityCache.OnIdentityCleared -= CancelUpdateOperation;
            web3IdentityCache.OnIdentityChanged -= CancelUpdateOperation;
            publishProfileCts?.SafeCancelAndDispose();
        }

        private void UnEquipAll()
        {
            equippedEmotes.UnEquipAll();
            equippedWearables.UnEquipAll();
            equippedWearables.SetForceRender(Array.Empty<string>());
        }

        private void UnEquipAllWearables()
        {
            equippedWearables.UnEquipAll();
            equippedWearables.SetForceRender(Array.Empty<string>());
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

            var currentForceRender = new List<string>(equippedWearables.ForceRenderCategories);
            if (currentForceRender.Remove(wearable.GetCategory()))
            {
                equippedWearables.SetForceRender(currentForceRender);
                backpackEventBus.SendForceRender(currentForceRender);
            }
        }

        private void SetForceRender(IReadOnlyCollection<string> categories)
        {
            equippedWearables.SetForceRender(categories);
        }

        private void ChangeColor(Color newColor, string category)
        {
            switch (category)
            {
                case WearableCategories.Categories.EYES:
                    equippedWearables.SetEyesColor(newColor);
                    break;
                case WearableCategories.Categories.HAIR:
                    equippedWearables.SetHairColor(newColor);
                    break;
                case WearableCategories.Categories.BODY_SHAPE:
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
            bool publishProfileChange = !appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS)
                                        && !appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_WEARABLES);

            Profile? oldProfile = await selfProfile.ProfileAsync(ct);

            if (oldProfile == null)
            {
                ShowErrorNotificationAsync(ct).Forget();
                return;
            }

            if (!publishProfileChange)
            {
                var forceRenderList = new List<string>(equippedWearables.ForceRenderCategories);
                
                Profile newProfile = oldProfile.CreateNewProfileForUpdate(equippedEmotes, equippedWearables,
                    forceRenderList, emoteStorage, wearableStorage);

                // Skip publishing the same profile
                if (newProfile.IsSameProfile(oldProfile))
                {
                    ReportHub.LogWarning(ReportCategory.PROFILE, "Profile update skipped - no changes detected in avatar configuration");
                    return;
                }

                profileCache.Set(newProfile.UserId, newProfile);
                UpdateAvatarInWorld(newProfile);
                profileChangesBus.PushUpdate(newProfile);
                LogSystemState("[ProfileUpdate [Local] - debug snapshot", newProfile.UserId, newProfile.WalletId);
                
                return;
            }

            try
            {
                Profile? newProfile = await selfProfile.UpdateProfileAsync(ct, updateAvatarInWorld: true);
                LogSystemState("[ProfileUpdate - [Published] - debug snapshot", newProfile?.UserId, newProfile?.WalletId);
                MultithreadingUtility.AssertMainThread(nameof(UpdateProfileAsync), true);

                if (newProfile != null)
                    profileChangesBus.PushUpdate(newProfile);
            }
            catch (OperationCanceledException) { }
            catch (IdenticalProfileUpdateException)
            {
                ReportHub.LogWarning(ReportCategory.PROFILE, "Profile update skipped - no changes detected");
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.PROFILE);
                LogSystemState("[ProfileUpdate - [FAILED] - debug snapshot", oldProfile.UserId, oldProfile.WalletId);
                ShowErrorNotificationAsync(ct).Forget();
            }
        }

        private void LogSystemState(string source, string? userId, string? walletId)
        {
            var debugInfo = new StringBuilder();

            debugInfo.AppendLine($"----------- {source} (User: {userId} | Wallet: {walletId}) -----------");
            debugInfo.AppendLine($"Profile state {userId} wallet {walletId}");
            debugInfo.AppendLine($"RealmData state {userId} {realmData}");

            foreach ((string category, var w) in equippedWearables.Items())
            {
                if (w == null) continue;

                var shortUrn = w.GetUrn();
                debugInfo.Append($"  - Cat: {category,-15} | Short URN: '{shortUrn}'");
                if (wearableStorage.TryGetOwnedNftRegistry(shortUrn, out var registry) && registry.Count > 0)
                {
                    var fullUrn = registry.First().Value.Urn;
                    debugInfo.AppendLine($" -> [FOUND] Full URN: '{fullUrn}'");
                }
                else
                {
                    debugInfo.AppendLine(" -> [NOT FOUND] No full URN mapping exists in the registry yet!");
                }
            }

            ReportHub.Log(ReportCategory.OUTFITS, debugInfo.ToString());
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

        private void CancelUpdateOperation() =>
            publishProfileCts?.SafeCancelAndDispose();
    }
}
