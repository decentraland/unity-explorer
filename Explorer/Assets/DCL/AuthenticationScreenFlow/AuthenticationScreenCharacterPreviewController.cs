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

        private readonly List<URN> shortenedWearables = new ();
        private readonly HashSet<URN> shortenedEmotes = new ();

        private URN[] previewEmotes;
        private float emoteCooldown;

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
            previewEmotes = shortenedEmotes.ToArray();

            base.Initialize(avatar);

            PlayEmote("raiseHand");
            EmoteTaskLoop().Forget();
        }

        private async UniTask EmoteTaskLoop()
        {
            var i = 0;

            while (true)
            {

                if(emoteCooldown > TIME_BETWEEN_EMOTES)
                {
                    emoteCooldown = 0f;
                    // PlayEmote(previewEmotes[i]);
                    await PlayEmoteAndAwaitIt("disco");

                    i++;
                    if (i >= previewEmotes.Length)
                        i = 0;
                }

                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate);
                emoteCooldown += Time.deltaTime;
            }
        }

        public async UniTask  PlayJumpInEmoteAndAwaitIt() =>
            await PlayEmoteAndAwaitIt("fistpump");
    }
}
