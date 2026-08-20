using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Passport
{
    public class PassportCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly List<URN> shortenedWearables = new ();

        private CancellationTokenSource? emotePreviewCts;
        private bool isEmoteLoading;

        public PassportCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, false, characterPreviewEventBus) { }

        public override void Initialize(Avatar avatar, Vector3 position)
        {
            shortenedWearables.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            previewAvatarModel.Wearables = shortenedWearables;

            base.Initialize(avatar, position);
            PlayEmote("wave");
        }

        public void PlayEmoteClicked(URN emoteUrn)
        {
            emotePreviewCts = emotePreviewCts.SafeRestart();
            EnsureEmoteLoadedAndPlayAsync(emoteUrn.Shorten(), emotePreviewCts.Token).Forget();
        }

        public void StopEmotePreview()
        {
            bool restoreAfterCancelledLoad = isEmoteLoading;

            emotePreviewCts.SafeCancelAndDispose();
            StopEmotes();

            if (restoreAfterCancelledLoad)
                OnModelUpdated();
        }

        public new void OnHide(bool triggerOnHideBusEvent = true)
        {
            emotePreviewCts.SafeCancelAndDispose();
            base.OnHide(triggerOnHideBusEvent);
        }

        public new void Dispose()
        {
            emotePreviewCts.SafeCancelAndDispose();
            base.Dispose();
        }

        private async UniTaskVoid EnsureEmoteLoadedAndPlayAsync(URN urn, CancellationToken ct)
        {
            previewAvatarModel.Emotes ??= new HashSet<URN>();

            if (!previewAvatarModel.Emotes.Contains(urn))
            {
                previewAvatarModel.Emotes.Add(urn);
                isEmoteLoading = true;

                try { await ShowLoadingSpinnerAndUpdateAvatarAsync(ct); }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.EMOTE); }
                finally
                {
                    previewAvatarModel.Emotes.Remove(urn);
                    isEmoteLoading = false;
                }
            }

            if (ct.IsCancellationRequested)
                return;

            PlayEmote(urn);
        }
    }
}
