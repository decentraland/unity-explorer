using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
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
        [field: SerializeField] private Color NoProfileColor { get; set; }

        private UserWalletAddressElementController? creatorAddressController;

        private void Awake()
        {
            creatorAddressController = new UserWalletAddressElementController(creatorAddressElement);
        }

        public void ConfigurePanel(Profile? profile,
            string sceneCreatorAddress,
            decimal donationAmount,
            ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            titleText.text = string.Format(TITLE_FORMAT, donationAmount);

            userNameElement.gameObject.SetActive(profile != null);

            if (profile != null)
            {
                profilePictureView.Setup(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);
                userNameElement.Setup(profile);
            }
            else
            {
                profilePictureView.SetBackgroundColor(NoProfileColor);
                profilePictureView.SetDefaultThumbnail();
            }

            creatorAddressController!.Setup(sceneCreatorAddress);
        }
    }
}
