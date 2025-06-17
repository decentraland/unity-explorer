using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.CharacterPreview;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenCharacterPreviewController : CharacterPreviewControllerBase
    {
        private const float TIME_BETWEEN_EMOTES = 3f;
        private const string INTRO_EMOTE_URN = "raiseHand";
        private const string JUMP_IN_EMOTE_URN = "fistpump";
        private static readonly URN[] FUNNY_EMOTES = { new ("disco"), new ("robot") };

        private readonly List<URN> shortenedWearables = new();
        private readonly HashSet<URN> shortenedEmotes = new();
        private readonly HashSet<URN> previewEmotesSet = new ();

        private URN[] previewEmotes;
        private float emoteCooldown;
        private int currentEmoteIndex;

        public AuthenticationScreenCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, true, characterPreviewEventBus) { }

        public override void Initialize(Avatar avatar)
        {
            previewAvatarModel.Wearables = ShortenWearables(avatar);
            previewAvatarModel.Emotes = ShortenEmotes(avatar);

            previewEmotes = PreviewEmotes();
            RandomizePreviewEmotes();

            base.Initialize(avatar);

            PlayPreviewEmotesSequentially().Forget();
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

            foreach (var funnyEmote in FUNNY_EMOTES)
                previewEmotesSet.Add(funnyEmote);

            return previewEmotesSet.ToArray();
        }

        // Fisher-Yates shuffle algorithm
        private void RandomizePreviewEmotes()
        {
            for (int i = previewEmotes.Length - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (previewEmotes[i], previewEmotes[randomIndex]) = (previewEmotes[randomIndex], previewEmotes[i]);
            }
        }

        private async UniTask PlayPreviewEmotesSequentially()
        {
            await PlayEmoteAndAwaitIt(INTRO_EMOTE_URN);

            if (previewEmotes is not { Length: > 0 }) return;

            currentEmoteIndex = 0;
            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate);
                emoteCooldown += Time.deltaTime;

                if (emoteCooldown > TIME_BETWEEN_EMOTES)
                {
                    await PlayEmoteAndAwaitIt(previewEmotes[currentEmoteIndex]);

                    emoteCooldown = 0f;
                    currentEmoteIndex = (currentEmoteIndex + 1) % previewEmotes.Length;
                }
            }
        }

        public async UniTask PlayJumpInEmoteAndAwaitIt() =>
            await PlayEmoteAndAwaitIt(JUMP_IN_EMOTE_URN);
    }
}
