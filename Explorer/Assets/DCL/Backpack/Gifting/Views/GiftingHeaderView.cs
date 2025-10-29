using DCL.UI.ProfileElements;
using TMPro;
using UnityEngine;

namespace DCL.Backpack.Gifting.Views
{
    public class GiftingHeaderView : MonoBehaviour
    {
        [field: SerializeField]
        public TextMeshProUGUI Title { get; private set; }

        [field: SerializeField]
        public ProfilePictureView UserProfileImage { get; private set; }

        [field: SerializeField]
        public UserWalletAddressElement UserProfileWallet { get; private set; }
    }
}