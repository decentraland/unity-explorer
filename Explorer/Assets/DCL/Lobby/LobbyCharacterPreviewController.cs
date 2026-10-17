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
using Random = UnityEngine.Random;

namespace DCL.Lobby
{
    /// <summary>
    ///     Preview of the player's own avatar shown in the lobby on the 3D stage; it idles and plays a flavour emote every now and then.
    /// </summary>
    public class LobbyCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly LobbyAvatarSettings settings;
        private readonly LobbyStage stage;

        private readonly List<URN> shortenedWearables = new ();
        private readonly HashSet<URN> shortenedEmotes = new ();

        private CancellationTokenSource? emotesCts;
        private Avatar? appliedAvatar;

        public LobbyCharacterPreviewController(CharacterPreviewView view, LobbyAvatarSettings settings, LobbyStage stage, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, isPreviewPlatformActive: false, characterPreviewEventBus)
        {
            this.settings = settings;
            this.stage = stage;

            RenderTargetChanged += FitStage;

            panEnabled = false;
            zoomEnabled = false;
        }

        // The stage renders into the same target and is the lobby's backdrop, so it has to be on screen while the figure still loads
        protected override bool hideImageWhileLoading => false;

        public override void Initialize(Avatar avatar, Vector3 position)
        {
            view.gameObject.SetActive(true);

            stage.transform.position = position;
            stage.gameObject.SetActive(true);

            ApplyAvatar(avatar, position);

            emotesCts = emotesCts.SafeRestart();
            PlayFlavourEmotesAsync(emotesCts.Token).Forget();
        }

        /// <summary>
        ///     Re-dresses the avatar and celebrates with an emote; an identical look is ignored so the catalyst confirming it does not replay.
        /// </summary>
        public void Refresh(Avatar avatar)
        {
            if (avatar.IsSameAvatar(appliedAvatar)) return;

            ApplyAvatar(avatar, CharacterPreviewUtils.LOBBY_PREVIEW_POSITION);

            emotesCts = emotesCts.SafeRestart();
            ReloadAndCelebrateAsync(emotesCts.Token).Forget();
        }

        public void SetHovered(bool hovered) =>
            previewController?.SetHovered(hovered);

        public override void OnHide(bool triggerOnHideBusEvent = true)
        {
            emotesCts.SafeCancelAndDispose();
            base.OnHide(triggerOnHideBusEvent);

            view.gameObject.SetActive(false);
            stage.gameObject.SetActive(false);
        }

        public override void Dispose()
        {
            emotesCts.SafeCancelAndDispose();
            RenderTargetChanged -= FitStage;
            base.Dispose();
        }

        // The pooled camera can change with every render target, so the stage is pointed at the current one each time
        private void FitStage()
        {
            SetPostProcessingEnabled(settings.PostProcessing);

            // The stage brings its own preset-driven light; the container is pooled, so this runs for every new preview
            SetPreviewLightActive(false);
            stage.Track(previewCamera);
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
            appliedAvatar = avatar;

            base.Initialize(avatar, position);
        }

        // Nothing opaque sits behind the full-screen image, so the current look stays on until the new one is instantiated instead of showing the world through
        private async UniTaskVoid ReloadAndCelebrateAsync(CancellationToken ct)
        {
            try
            {
                if (previewController != null)
                    await previewController.Value.UpdateAvatarAsync(previewAvatarModel, ct);

                PlayEmote(settings.ProfileUpdatedEmoteURN);
                await PlayFlavourEmotesAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
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

        [Tooltip("Runs the URP post-processing volume on the lobby preview camera; other previews keep it off")]
        [field: SerializeField] public bool PostProcessing { get; private set; }
    }
}
