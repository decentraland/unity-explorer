using DCL.NotificationsBusController.NotificationTypes;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationEntry
{
    public class SystemNotificationView: MonoBehaviour, IPointerClickHandler
    {
        public event Action<NotificationType, string> NotificationClicked;
        public NotificationType NotificationType { get; set; }
        public string NotificationId { get; set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public TMP_Text HeaderText { get; private set; }

        [field: SerializeField]
        public Image NotificationTypeImage { get; private set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            NotificationClicked?.Invoke(NotificationType, NotificationId);
        }
    }
}
