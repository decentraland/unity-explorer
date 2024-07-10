using System;

namespace DCL.Notification.NotificationsBus
{
    public interface INotificationsBusController
    {
        public event Action<INotification> OnNotificationAdded;

        void AddNotification(INotification notification);
    }
}
