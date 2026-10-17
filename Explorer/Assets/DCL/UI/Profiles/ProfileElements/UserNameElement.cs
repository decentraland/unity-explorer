using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileElements
{
    public class UserNameElement : MonoBehaviour
    {
        [field: SerializeField] public RectTransform UserNameContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text UserNameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text UserNameHashtagText { get; private set; } = null!;
        [field: SerializeField] public GameObject VerifiedMark { get; private set; } = null!;
        [field: SerializeField] public GameObject OfficialMark { get; private set; } = null!;
        [field: SerializeField] public Button CopyUserNameButton { get; private set; } = null!;
        [field: SerializeField] public WarningNotificationView CopyNameWarningNotification { get; private set; } = null!;
    }
}
