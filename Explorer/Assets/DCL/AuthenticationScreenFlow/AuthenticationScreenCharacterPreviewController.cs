using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.CharacterPreview;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly List<URN> shortenedWearables = new ();

        public AuthenticationScreenCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world)
            : base(view, previewFactory, world) { }

        public override void Initialize(Avatar avatar)
        {
            shortenedWearables.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            previewAvatarModel.Wearables = shortenedWearables;

            base.Initialize(avatar);
        }
    }
}
