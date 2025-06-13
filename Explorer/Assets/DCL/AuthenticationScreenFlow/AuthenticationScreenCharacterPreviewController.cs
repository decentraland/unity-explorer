using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Loading.Components;
using DCL.CharacterPreview;
using System;
using System.Collections.Generic;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly List<URN> shortenedWearables = new ();
        private readonly HashSet<URN> shortenedEmotes = new ();
        private readonly IEquippedEmotes equippedEmotes;
        private Avatar avatar;

        public AuthenticationScreenCharacterPreviewController(CharacterPreviewView view,  IEquippedEmotes equippedEmotes, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, true, characterPreviewEventBus)
        {
            this.equippedEmotes = equippedEmotes;
        }

        public override void Initialize(Avatar avatar)
        {
            this.avatar = avatar;

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

            base.Initialize(avatar);

            EmoteTaskLoop().Forget();
            checkTime = Time.unscaledTime;
        }

        private float checkTime;

        private async UniTask EmoteTaskLoop()
        {
            int i = 0;

            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate);
                if(Time.unscaledTime - checkTime > 3f)
                {
                    checkTime = Time.unscaledTime;
                    PlayEmote(avatar.Emotes[i].Shorten());

                    i++;
                    if (i >= avatar.Emotes.Count)
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
