using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly List<URN> shortenedWearables = new ();
        private readonly HashSet<URN> shortenedEmotes = new ();

        private URN[] cachedShortenedEmotes;

        public AuthenticationScreenCharacterPreviewController(CharacterPreviewView view,  ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, true, characterPreviewEventBus) { }

        public override void Initialize(Avatar avatar)
        {
            shortenedWearables.Clear();
            shortenedEmotes.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            foreach (URN urn in avatar.Emotes)
            {
                if (urn.IsNullOrEmpty()) continue;
                URN shortenedUrn = urn.Shorten();

                shortenedEmotes.Add(shortenedUrn);
            }

            previewAvatarModel.Wearables = shortenedWearables;
            previewAvatarModel.Emotes = shortenedEmotes;
            cachedShortenedEmotes = shortenedEmotes.ToArray();

            base.Initialize(avatar);

            PlayEmote("wave");
            EmoteTaskLoop().Forget();
        }

        private const float TIME_BETWEEN_EMOTES = 3f;
        private float emoteCooldown;

        private async UniTask EmoteTaskLoop()
        {
            int i = 0;

            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate);

                if (!IsPlayingEmote())
                    emoteCooldown += Time.deltaTime;

                if(emoteCooldown > TIME_BETWEEN_EMOTES)
                {
                    emoteCooldown = 0f;
                    PlayEmote(cachedShortenedEmotes[i]);

                    i++;
                    if (i >= cachedShortenedEmotes.Length)
                        i = 0;
                }
            }
        }

        public int PlayJumpInEmote()
        {
            PlayEmote("wave");
            return 3;
        }
    }
}
