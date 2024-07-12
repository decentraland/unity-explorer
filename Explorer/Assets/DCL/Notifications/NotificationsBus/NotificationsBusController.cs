using System;
using UnityEngine;

namespace DCL.Notification.NotificationsBus
{
    public class NotificationsBusController : INotificationsBusController
    {
        public event Action<INotification> OnNotificationAdded;
        public event Action<NotificationType> OnNotificationClicked;

        public void AddNotification(INotification notification)
        {
            OnNotificationAdded?.Invoke(notification);
        }

        public void ClickNotification(NotificationType notificationType)
        {
            OnNotificationClicked?.Invoke(notificationType);
        }
    }
}
