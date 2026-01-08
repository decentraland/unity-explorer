using DCL.Profiles;
using DCL.UI.EphemeralNotifications;
using DCL.UI.ProfileElements;
using UnityEngine;

namespace DCL.SocialEmotes.UI
{
    public class FriendOnlineEphemeralNotification : AbstractEphemeralNotification
    {
        [SerializeField]
        private ProfilePictureView profilePictureView;

        public override void SetData(Profile.CompactInfo sender, string[] textValues)
        {
            base.SetData(sender, textValues);

            if(sender.ProfilePicture != null)
                profilePictureView.SetImage(sender.ProfilePicture.Value.Asset.Sprite);

            profilePictureView.SetBackgroundColor(sender.UserNameColor);
        }
    }
}
