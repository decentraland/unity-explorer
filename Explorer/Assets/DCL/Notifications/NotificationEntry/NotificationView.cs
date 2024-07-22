using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Notification.NotificationEntry
{
    public class NotificationView : MonoBehaviour, IPointerClickHandler
    {
        public event Action<NotificationType, string> NotificationClicked;
        public NotificationType NotificationType { get; set; }
        public string NotificationId { get; set; }

        [field: SerializeField]
        public GameObject UnreadImage { get; private set; }

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

        public void OnPointerClick(PointerEventData eventData)
        {
            NotificationClicked?.Invoke(NotificationType, NotificationId);
        }
    }
}
