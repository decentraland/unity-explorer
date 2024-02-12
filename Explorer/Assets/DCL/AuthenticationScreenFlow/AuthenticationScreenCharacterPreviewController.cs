using CommunicationData.URLHelpers;
using DCL.CharacterPreview;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenCharacterPreviewController : CharacterPreviewControllerBase
    {
        public AuthenticationScreenCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory) : base(view, previewFactory) { }

        public override void Initialize(Avatar avatar)
        {
            previewAvatarModel.Wearables = new List<URN>(avatar.SharedWearables);
            base.Initialize(avatar);
        }
    }
}
