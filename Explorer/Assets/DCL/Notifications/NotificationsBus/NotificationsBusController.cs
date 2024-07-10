using System;

namespace DCL.Notification.NotificationsBus
{
    public class NotificationsBusController : INotificationsBusController
    {
        public event Action<INotification> OnNotificationAdded;

        public void AddNotification(INotification notification)
        {
            OnNotificationAdded?.Invoke(notification);
        }
    }
}
