using Cysharp.Threading.Tasks;
using DCL.RewardPanel;
using DCL.UI.ProfileElements;
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
        [field: SerializeField] private SimpleUserNameElement userNameElement { get; set; } = null!;
        [field: SerializeField] private RewardBackgroundRaysAnimation rewardBackgroundRaysAnimation { get; set; } = null!;
        [field: SerializeField] private TMP_Text tipSentText { get; set; } = null!;
        [field: SerializeField] internal Button okButton { get; set; } = null!;
        [field: SerializeField] internal Button backgroundButton { get; set; } = null!;

        public async UniTask ShowAsync(DonationPanelViewModel viewModel, CancellationToken ct)
        {
            userNameElement.gameObject.SetActive(viewModel.Profile != null);

            profilePictureView.Bind(viewModel.ProfileThumbnail);

            if (!viewModel.Profile.HasValue)
            {
                tipSentText.text = string.Format(NO_PROFILE_TEXT_FORMAT, $"{viewModel.SceneCreatorAddress[..5]}...{viewModel.SceneCreatorAddress[^5..]}");
                profilePictureView.ConfigureThumbnailClickData(userAddress: viewModel.SceneCreatorAddress);
            }
            else
            {
                userNameElement.Setup(viewModel.Profile.Value);
                tipSentText.text = PROFILE_TEXT;
            }

            await rewardBackgroundRaysAnimation.ShowAnimationAsync(ct);
            await UniTask.WhenAny(okButton.OnClickAsync(ct), backgroundButton.OnClickAsync(ct));
            await rewardBackgroundRaysAnimation.HideAnimationAsync(ct);
        }
    }
}
