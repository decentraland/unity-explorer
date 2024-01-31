using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.CharacterPreview
{
    public class AuthenticationScreenCharacterPreviewController : CharacterPreviewControllerBase
    {
        public AuthenticationScreenCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory) : base(view, previewFactory)
        {
        }

        public override void Initialize(Avatar avatar)
        {
            previewAvatarModel.Wearables = new List<string>();
            foreach (var wearable in avatar.SharedWearables)
            {
                previewAvatarModel.Wearables.Add(wearable);
            }

            base.Initialize(avatar);
        }
    }
}
