using System;

namespace DCL.Notification.NotificationsBus
{
    public interface INotificationsBusController
    {
        public event Action<INotification> OnNotificationAdded;
        public event Action<NotificationType> OnNotificationClicked;

        void AddNotification(INotification notification);

        void ClickNotification(NotificationType notificationType);
    }
}
