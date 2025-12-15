using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.RewardPanel;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Donations.UI
{
    public class DonationConfirmedView : MonoBehaviour
    {
        private const string NO_PROFILE_TEXT_FORMAT = "Tip Sent to {0}";
        private const string PROFILE_TEXT = "Tip Sent to";

        [field: SerializeField] private ProfilePictureView profilePictureView { get; set; } = null!;
        [field: SerializeField] private Color NoProfileColor { get; set; }
        [field: SerializeField] private SimpleUserNameElement userNameElement { get; set; } = null!;
        [field: SerializeField] private RewardBackgroundRaysAnimation rewardBackgroundRaysAnimation { get; set; } = null!;
        [field: SerializeField] private TMP_Text tipSentText { get; set; } = null!;
        [field: SerializeField] internal Button okButton { get; set; } = null!;
        [field: SerializeField] internal Button backgroundButton { get; set; } = null!;

        public async UniTask ShowAsync(Profile? profile, string creatorAddress, CancellationToken ct, ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            userNameElement.gameObject.SetActive(profile != null);

            if (profile == null)
            {
                profilePictureView.SetBackgroundColor(NoProfileColor);
                profilePictureView.SetDefaultThumbnail();
                tipSentText.text = string.Format(NO_PROFILE_TEXT_FORMAT, $"{creatorAddress[..5]}...{creatorAddress[^5..]}");
                profilePictureView.ConfigureThumbnailClickData(userAddress: creatorAddress);
            }
            else
            {
                profilePictureView.Setup(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);
                userNameElement.Setup(profile);
                tipSentText.text = PROFILE_TEXT;
            }

            await rewardBackgroundRaysAnimation.ShowAnimationAsync(ct);
            await UniTask.WhenAny(okButton.OnClickAsync(ct), backgroundButton.OnClickAsync(ct));
            await rewardBackgroundRaysAnimation.HideAnimationAsync(ct);
        }
    }
}
