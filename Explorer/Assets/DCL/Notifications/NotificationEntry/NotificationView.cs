using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Notification.NotificationEntry
{
    public class NotificationView : MonoBehaviour
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public TMP_Text HeaderText { get; private set; }

        [field: SerializeField]
        public TMP_Text TitleText { get; private set; }

        [field: SerializeField]
        public TMP_Text TimeText { get; private set; }

        [field: SerializeField]
        public ImageView NotificationImage { get; private set; }

        [field: SerializeField]
        public Image NotificationImageBackground { get; private set; }

        [field: SerializeField]
        public Image NotificationTypeImage { get; private set; }
    }
}
