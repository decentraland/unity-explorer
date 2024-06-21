using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.CharacterPreview;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.Passport
{
    public class PassportCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly List<URN> shortenedWearables = new ();
        private readonly HashSet<URN> shortenedEmotes = new ();

        public PassportCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world)
            : base(view, previewFactory, world, false) { }

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

                if (!shortenedEmotes.Contains(shortenedUrn))
                    shortenedEmotes.Add(shortenedUrn);
            }

            previewAvatarModel.Wearables = shortenedWearables;
            previewAvatarModel.Emotes = shortenedEmotes;

            base.Initialize(avatar);
            base.PlayEmote("wave");
        }
    }
}
