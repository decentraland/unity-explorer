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

        public PassportCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world)
            : base(view, previewFactory, world, false) { }

        public override void Initialize(Avatar avatar)
        {
            shortenedWearables.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            previewAvatarModel.Wearables = shortenedWearables;

            base.Initialize(avatar);
            base.PlayEmote("wave");
        }
    }
}
