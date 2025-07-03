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

            base.Initialize(avatar);
            previewController!.Value.AddHeadIK();

            playEmotesCts = playEmotesCts.SafeRestart();
            PlayEmoteAndAwaitItAsync(settings.IntroEmoteURN, playEmotesCts.Token).Forget();
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

        public async UniTask PlayJumpInEmoteAndAwaitItAsync()
        {
            playEmotesCts = playEmotesCts.SafeRestart();
            await PlayEmoteAndAwaitItAsync(settings.JumpInEmoteURN, playEmotesCts!.Token);
        }
    }

    [Serializable]
    public class AuthScreenEmotesSettings
    {
        [field: SerializeField] public string IntroEmoteURN { get; private set; }
        [field: SerializeField] public string JumpInEmoteURN { get; private set; }
    }
}
