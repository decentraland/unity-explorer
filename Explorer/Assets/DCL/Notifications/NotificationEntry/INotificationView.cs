using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationEntry
{
    public interface INotificationView
    {
        event Action<NotificationType, INotification> NotificationClicked;
        NotificationType NotificationType { get; set; }
        INotification Notification { get; set; }
        TMP_Text HeaderText { get; set; }
        TMP_Text TitleText { get; set; }
        TMP_Text TimeText { get; set; }
        Button CloseButton { get; set; }
        GameObject UnreadImage { get; set; }
        Image NotificationTypeImage { get; set; }
        ImageView NotificationImage { get; set; }
        Image NotificationImageBackground { get; set; }
    }
}
