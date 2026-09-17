using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Avatar = DCL.Profiles.Avatar;
using Random = UnityEngine.Random;

namespace DCL.Lobby
{
    /// <summary>
    ///     Camera-facing preview of the player's own avatar shown in the lobby.
    ///     It idles by default and plays a flavour emote every now and then.
    /// </summary>
    public class LobbyCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly LobbyAvatarSettings settings;

        private readonly List<URN> shortenedWearables = new ();
        private readonly HashSet<URN> shortenedEmotes = new ();

        private CancellationTokenSource? emotesCts;

        public LobbyCharacterPreviewController(CharacterPreviewView view, LobbyAvatarSettings settings, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, true, characterPreviewEventBus)
        {
            this.settings = settings;

            rotateEnabled = false;
            panEnabled = false;
            zoomEnabled = false;
        }

        public override void Initialize(Avatar avatar, Vector3 position)
        {
            view.gameObject.SetActive(true);

            ApplyAvatar(avatar, position);

            emotesCts = emotesCts.SafeRestart();
            PlayFlavourEmotesAsync(emotesCts.Token).Forget();
        }

        /// <summary>
        ///     Re-dresses the avatar with an updated profile and plays an emote once the new look is loaded.
        /// </summary>
        public void Refresh(Avatar avatar)
        {
            ApplyAvatar(avatar, CharacterPreviewUtils.LOBBY_PREVIEW_POSITION);

            emotesCts = emotesCts.SafeRestart();
            ReloadAndCelebrateAsync(emotesCts.Token).Forget();
        }

        public override void OnHide(bool triggerOnHideBusEvent = true)
        {
            emotesCts.SafeCancelAndDispose();
            base.OnHide(triggerOnHideBusEvent);

            view.gameObject.SetActive(false);
        }

        public override void Dispose()
        {
            emotesCts.SafeCancelAndDispose();
            base.Dispose();
        }

        private void ApplyAvatar(Avatar avatar, Vector3 position)
        {
            shortenedWearables.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            shortenedEmotes.Clear();

            foreach (URN urn in avatar.Emotes)
            {
                if (urn.IsNullOrEmpty()) continue;
                shortenedEmotes.Add(urn.Shorten());
            }

            previewAvatarModel.Wearables = shortenedWearables;
            previewAvatarModel.Emotes = shortenedEmotes;

            base.Initialize(avatar, position);
        }

        private async UniTaskVoid ReloadAndCelebrateAsync(CancellationToken ct)
        {
            try
            {
                await ShowLoadingSpinnerAndUpdateAvatarAsync(ct);
                PlayEmote(settings.ProfileUpdatedEmoteURN);
                await PlayFlavourEmotesAsync(ct);
            }
            catch (OperationCanceledException) { }
        }

        private async UniTask PlayFlavourEmotesAsync(CancellationToken ct)
        {
            if (settings.FlavourEmotes.Length == 0) return;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    float seconds = Random.Range(settings.MinSecondsBetweenFlavourEmotes, settings.MaxSecondsBetweenFlavourEmotes);
                    await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);

                    if (previewController is { } preview && preview.IsAvatarLoaded() && !preview.IsPlayingEmote())
                        PlayEmote(settings.FlavourEmotes[Random.Range(0, settings.FlavourEmotes.Length)]);
                }
            }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    ///     Emote ids are embedded (base) emote ids, the same the authentication screen uses.
    /// </summary>
    [Serializable]
    public class LobbyAvatarSettings
    {
        [field: SerializeField] public string[] FlavourEmotes { get; private set; } = { "wave", "fistpump", "dab" };
        [field: SerializeField, Min(0f)] public float MinSecondsBetweenFlavourEmotes { get; private set; } = 8f;
        [field: SerializeField, Min(0f)] public float MaxSecondsBetweenFlavourEmotes { get; private set; } = 20f;
        [field: SerializeField] public string ProfileUpdatedEmoteURN { get; private set; } = "fistpump";
    }
}
