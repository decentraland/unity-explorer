using DCL.NotificationsBusController.NotificationTypes;
using System;
using System.Collections.Generic;

namespace DCL.NotificationsBusController.NotificationsBus
{
    public class NotificationsBusController : INotificationsBusController
    {
        public delegate void NotificationClickedDelegate(params object[] parameters);
        public delegate void NotificationReceivedDelegate(INotification notification);

        private readonly Dictionary<NotificationType, NotificationClickedDelegate> notificationClickedSubscribers = new();
        private readonly Dictionary<NotificationType, NotificationReceivedDelegate> notificationReceivedSubscribers = new();

        public void AddNotification(INotification notification)
        {
            if (notificationReceivedSubscribers.TryGetValue(notification.Type, out NotificationReceivedDelegate thisEvent))
                thisEvent.Invoke(notification);
        }

        public void ClickNotification(NotificationType notificationType, params object[] parameters)
        {
            if (notificationClickedSubscribers.TryGetValue(notificationType, out NotificationClickedDelegate thisEvent))
                thisEvent.Invoke(parameters);
        }

        public void SubscribeToNotificationTypeClick(NotificationType desiredType, NotificationClickedDelegate listener)
        {
            notificationClickedSubscribers.TryGetValue(desiredType, out NotificationClickedDelegate thisEvent);
            thisEvent += listener;
            notificationClickedSubscribers[desiredType] = thisEvent;
        }

        public void SubscribeToNotificationTypeReceived(NotificationType desiredType, NotificationReceivedDelegate listener)
        {
            notificationReceivedSubscribers.TryGetValue(desiredType, out NotificationReceivedDelegate thisEvent);
            thisEvent += listener;
            notificationReceivedSubscribers[desiredType] = thisEvent;
        }

        public void SubscribeToAllNotificationTypesClick(NotificationClickedDelegate listener)
        {
            foreach (NotificationType notificationType in Enum.GetValues(typeof(NotificationType)))
            {
                notificationClickedSubscribers.TryGetValue(notificationType, out NotificationClickedDelegate thisEvent);
                thisEvent += listener;
                notificationClickedSubscribers[notificationType] = thisEvent;
            }
        }

        public void SubscribeToAllNotificationTypesReceived(NotificationReceivedDelegate listener)
        {
            foreach (NotificationType notificationType in Enum.GetValues(typeof(NotificationType)))
            {
                notificationReceivedSubscribers.TryGetValue(notificationType, out NotificationReceivedDelegate thisEvent);
                thisEvent += listener;
                notificationReceivedSubscribers[notificationType] = thisEvent;
            }
        }
    }
}
