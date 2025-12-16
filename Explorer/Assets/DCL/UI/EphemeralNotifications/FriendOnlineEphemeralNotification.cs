using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.EphemeralNotifications;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;

namespace DCL.SocialEmotes.UI
{
    public class FriendOnlineEphemeralNotification : AbstractEphemeralNotification
    {
        [SerializeField]
        private ProfilePictureView profilePictureView;

        private readonly ReactiveProperty<ProfileThumbnailViewModel> profileThumbnailViewModel = new (ProfileThumbnailViewModel.Default());

        public override void SetData(Profile.CompactInfo sender, string[] textValues)
        {
            base.SetData(sender, textValues);

            profilePictureView.Bind(profileThumbnailViewModel, sender.UserNameColor);
            GetProfileThumbnailCommand.Instance.ExecuteAsync(profileThumbnailViewModel, null, sender.UserId, sender.FaceSnapshotUrl, destroyCancellationToken).Forget();
        }
    }
}
