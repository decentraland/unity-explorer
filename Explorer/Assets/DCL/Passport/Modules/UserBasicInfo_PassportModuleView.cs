using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserBasicInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform UserNameContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text UserNameText { get; private set; }

        [field: SerializeField]
        public GameObject VerifiedMark { get; private set; }

        [field: SerializeField]
        public Button CopyUserNameButton { get; private set; }

        [field: SerializeField]
        public WarningNotificationView CopyNameWarningNotification { get; private set; }

        [field: SerializeField]
        public RectTransform WalletAddressContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text UserWalletAddressText { get; private set; }

        [field: SerializeField]
        public Button CopyWalletAddressButton { get; private set; }

        [field: SerializeField]
        public WarningNotificationView CopyWalletWarningNotification { get; private set; }
    }
}
