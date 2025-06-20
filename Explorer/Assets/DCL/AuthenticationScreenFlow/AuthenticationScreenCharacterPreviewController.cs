using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;
using Avatar = DCL.Profiles.Avatar;
using Random = UnityEngine.Random;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly AuthScreenEmotesSettings settings;

        private readonly List<URN> shortenedWearables = new();
        private readonly HashSet<URN> shortenedEmotes = new();
        private readonly HashSet<URN> previewEmotesSet = new ();

        private URN[] previewEmotes;
        private float emoteCooldown;
        private int currentEmoteIndex;
        private CancellationTokenSource? playEmotesCts;

        public AuthenticationScreenCharacterPreviewController(CharacterPreviewView view, AuthScreenEmotesSettings settings, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, true, characterPreviewEventBus)
        {
            this.settings = settings;
        }

        public override void Initialize(Avatar avatar)
        {
            previewAvatarModel.Wearables = ShortenWearables(avatar);
            previewAvatarModel.Emotes = ShortenEmotes(avatar);

            previewEmotes = PreviewEmotes();
            RandomizePreviewEmotes();

            base.Initialize(avatar);

            playEmotesCts = playEmotesCts.SafeRestart();
            PlayPreviewEmotesSequentiallyAsync(playEmotesCts.Token).Forget();
        }

        public new void OnHide(bool triggerOnHideBusEvent = true)
        {
            playEmotesCts.SafeCancelAndDispose();
            base.OnHide(triggerOnHideBusEvent);
        }

        public new void Dispose()
        {
            playEmotesCts.SafeCancelAndDispose();
            base.Dispose();
        }

        private List<URN> ShortenWearables(Avatar avatar)
        {
            shortenedWearables.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            return shortenedWearables;
        }

        private HashSet<URN> ShortenEmotes(Avatar avatar)
        {
            shortenedEmotes.Clear();

            foreach (URN urn in avatar.Emotes)
            {
                if (urn.IsNullOrEmpty()) continue;
                URN shortenedUrn = urn.Shorten();
                shortenedEmotes.Add(shortenedUrn);
            }

            return shortenedEmotes;
        }

        private URN[] PreviewEmotes()
        {
            previewEmotesSet.Clear();

            foreach (var emote in shortenedEmotes)
                previewEmotesSet.Add(emote);

            foreach (string funnyEmote in settings.FunnyEmotes)
                previewEmotesSet.Add(funnyEmote);

            return previewEmotesSet.ToArray();
        }

        private async UniTask PlayPreviewEmotesSequentiallyAsync(CancellationToken ct)
        {
            await PlayEmoteAndAwaitItAsync(settings.IntroEmoteURN, ct);

            if (previewEmotes is { Length: <= 0 } || ct.IsCancellationRequested) return;

            currentEmoteIndex = 0;
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate);
                emoteCooldown += Time.deltaTime;

                if (emoteCooldown > settings.TimeBetweenEmotes)
                {
                    await PlayEmoteAndAwaitItAsync(previewEmotes[currentEmoteIndex], ct);

                    emoteCooldown = 0f;
                    currentEmoteIndex++;

                    if (currentEmoteIndex >= previewEmotes.Length)
                    {
                        RandomizePreviewEmotes();
                        currentEmoteIndex = 0;
                    }
                }
            }
        }

        public async UniTask PlayJumpInEmoteAndAwaitItAsync() =>
            await PlayEmoteAndAwaitItAsync(settings.JumpInEmoteURN, playEmotesCts!.Token);

        /// <summary>
        /// Fisher-Yates shuffle algorithm
        /// </summary>
        private void RandomizePreviewEmotes()
        {
            for (int i = previewEmotes.Length - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (previewEmotes[i], previewEmotes[randomIndex]) = (previewEmotes[randomIndex], previewEmotes[i]);
            }
        }
    }

    [Serializable]
    public class AuthScreenEmotesSettings
    {
        [field: SerializeField] public float TimeBetweenEmotes { get; private set; }
        [field: SerializeField] public string IntroEmoteURN { get; private set; }
        [field: SerializeField] public string JumpInEmoteURN { get; private set; }
        [field: SerializeField] public string[] FunnyEmotes { get; private set; }
    }
}
