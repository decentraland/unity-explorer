using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Notification.NotificationsBus
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
            if (notificationClickedSubscribers.TryGetValue(desiredType, out NotificationClickedDelegate thisEvent))
            {
                thisEvent += listener;
                notificationClickedSubscribers[desiredType] = thisEvent;
            }
            else
            {
                thisEvent += listener;
                notificationClickedSubscribers.Add(desiredType, thisEvent);
            }
        }

        public void SubscribeToNotificationTypeReceived(NotificationType desiredType, NotificationReceivedDelegate listener)
        {
            if (notificationReceivedSubscribers.TryGetValue(desiredType, out NotificationReceivedDelegate thisEvent))
            {
                thisEvent += listener;
                notificationReceivedSubscribers[desiredType] = thisEvent;
            }
            else
            {
                thisEvent += listener;
                notificationReceivedSubscribers.Add(desiredType, thisEvent);
            }
        }
    }
}
