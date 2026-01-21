using DCL.UI.ProfileElements;
using TMPro;
using UnityEngine;

namespace DCL.Donations.UI
{
    public class DonationLoadingView : MonoBehaviour
    {
        private const string TITLE_FORMAT = "Preparing {0} MANA Tip for";

        [field: SerializeField] private TMP_Text titleText { get; set; } = null!;
        [field: SerializeField] private ProfilePictureView profilePictureView { get; set; } = null!;
        [field: SerializeField] private SimpleUserNameElement userNameElement { get; set; } = null!;
        [field: Space(5)]
        [field: SerializeField] private UserWalletAddressElement creatorAddressElement { get; set; } = null!;

        private UserWalletAddressElementController? creatorAddressController;

        private void Awake()
        {
            creatorAddressController = new UserWalletAddressElementController(creatorAddressElement);
        }

        public void ConfigurePanel(DonationPanelViewModel viewModel,
            decimal donationAmount)
        {
            titleText.text = string.Format(TITLE_FORMAT, donationAmount);

            userNameElement.gameObject.SetActive(viewModel.Profile != null);

            profilePictureView.Bind(viewModel.ProfileThumbnail);

            if (viewModel.Profile.HasValue)
            {
                userNameElement.Setup(viewModel.Profile.Value);
                profilePictureView.ConfigureThumbnailClickData(userAddress: viewModel.SceneCreatorAddress);
            }

            creatorAddressController!.Setup(viewModel.SceneCreatorAddress);
        }
    }
}
