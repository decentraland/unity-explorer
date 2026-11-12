using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Shop
{
    /// <summary>
    ///     The player's own avatar shown next to the shop sections; the item panel dresses it with the open item and
    ///     <see cref="RestoreOwnAvatar" /> undresses it again.
    /// </summary>
    public class ShopCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly IWearableStorage wearableStorage;
        private readonly SelfProfile selfProfile;
        private readonly List<URN> baseWearables = new ();
        private readonly List<URN> modelWearables = new ();
        private readonly HashSet<URN> modelEmotes = new ();
        private readonly HashSet<string> modelForceRender = new ();

        private CancellationTokenSource? profileCts;
        private CancellationTokenSource? emoteCts;
        private Avatar? avatar;
        private bool tryOnActive;

        public BodyShape BodyShape => avatar?.BodyShape ?? BodyShape.MALE;

        public ShopCharacterPreviewController(
            CharacterPreviewView view,
            ICharacterPreviewFactory previewFactory,
            World world,
            CharacterPreviewEventBus characterPreviewEventBus,
            IWearableStorage wearableStorage,
            SelfProfile selfProfile)
            : base(view, previewFactory, world, true, characterPreviewEventBus)
        {
            this.wearableStorage = wearableStorage;
            this.selfProfile = selfProfile;
            zoomEnabled = false;
            panEnabled = false;
        }

        public override void Dispose()
        {
            base.Dispose();
            profileCts.SafeCancelAndDispose();
            emoteCts.SafeCancelAndDispose();
        }

        /// <summary>
        ///     Registers the preview on the bus right away and applies the own avatar once the profile resolves.
        /// </summary>
        public void Show()
        {
            OnBeforeShow();
            OnShow();
            profileCts = profileCts.SafeRestart();
            LoadOwnAvatarAsync(profileCts.Token).Forget();
        }

        public void Hide()
        {
            profileCts.SafeCancelAndDispose();
            StopEmotes();
            ResetModelToOwnAvatar();
            OnHide();
        }

        public override void Initialize(Avatar newAvatar, Vector3 position)
        {
            bool sameAvatar = avatar != null && avatar.IsSameAvatar(newAvatar);
            avatar = newAvatar;
            baseWearables.Clear();

            foreach (URN urn in newAvatar.Wearables)
                baseWearables.Add(urn.Shorten());

            StopEmotes();
            ResetModelToOwnAvatar();

            // The base only creates the preview once; afterwards it reads the model only through OnModelUpdated
            bool alreadyCreated = previewController != null;
            base.Initialize(newAvatar, position);

            if (alreadyCreated && !sameAvatar)
                OnModelUpdated();
        }

        public void TryOnWearable(IWearable wearable)
        {
            if (avatar == null)
                return;

            string category = wearable.GetCategory();

            if (category == WearableCategories.Categories.BODY_SHAPE)
                return;

            URN urn = wearable.GetUrn().Shorten();
            StopEmotes();
            ResetModelToOwnAvatar();
            modelWearables.Clear();

            foreach (URN equipped in baseWearables)
            {
                if (equipped.Equals(urn))
                    continue;

                if (wearableStorage.TryGetElement(equipped, out IWearable equippedWearable) && equippedWearable.GetCategory() == category)
                    continue;

                modelWearables.Add(equipped);
            }

            modelWearables.Add(urn);
            previewAvatarModel.ForceRenderCategories.Add(category);
            tryOnActive = true;
            OnModelUpdated();
        }

        public void TryOnEmote(URN emoteUrn)
        {
            if (avatar == null)
                return;

            // An emote already playing is not stopped: the stop blends out over several frames and would end the
            // emote started within them, whereas a new emote intent replaces the playing one directly
            ResetModelToOwnAvatar();
            tryOnActive = true;
            emoteCts = emoteCts.SafeRestart();
            PlayTryOnEmoteAsync(emoteUrn.Shorten(), emoteCts.Token).Forget();
        }

        public void RestoreOwnAvatar()
        {
            if (avatar == null || !tryOnActive)
                return;

            StopEmotes();
            ResetModelToOwnAvatar();
            OnModelUpdated();
        }

        private async UniTaskVoid LoadOwnAvatarAsync(CancellationToken ct)
        {
            ProfileReadResult readResult = await selfProfile.ProfileAsync(ct);

            if (ct.IsCancellationRequested)
                return;

            if (!readResult.IsOk(out Profile? profile))
            {
                ReportHub.LogWarning(ReportCategory.UI, "Own profile is not available, the shop cannot show the avatar");
                return;
            }

            Initialize(profile.Avatar, CharacterPreviewUtils.SHOP_PREVIEW_POSITION);
        }

        private async UniTaskVoid PlayTryOnEmoteAsync(URN urn, CancellationToken ct)
        {
            modelEmotes.Add(urn);

            try { await ShowLoadingSpinnerAndUpdateAvatarAsync(ct); }
            catch (OperationCanceledException) { return; }

            PlayEmote(urn);
        }

        // Puts the model back to the own avatar without re-rendering, so the caller decides when the avatar reloads
        private void ResetModelToOwnAvatar()
        {
            emoteCts.SafeCancelAndDispose();
            emoteCts = null;
            tryOnActive = false;

            modelWearables.Clear();
            modelWearables.AddRange(baseWearables);
            modelEmotes.Clear();
            previewAvatarModel.Wearables = modelWearables;
            previewAvatarModel.Emotes = modelEmotes;

            if (avatar == null)
                return;

            previewAvatarModel.ForceRenderCategories = modelForceRender;
            modelForceRender.Clear();

            foreach (string category in avatar.ForceRender)
                previewAvatarModel.ForceRenderCategories.Add(category);
        }
    }
}
