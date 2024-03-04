using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.CharacterPreview;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenCharacterPreviewController : CharacterPreviewControllerBase
    {
        public AuthenticationScreenCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world)
            : base(view, previewFactory, world) { }

        public override void Initialize(Avatar avatar)
        {
            previewAvatarModel.Wearables = new List<URN>(avatar.Wearables);
            base.Initialize(avatar);
        }
    }
}
