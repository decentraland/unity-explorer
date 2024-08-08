using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileElements
{
    public class UserWalletAddressElement : MonoBehaviour
    {
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
