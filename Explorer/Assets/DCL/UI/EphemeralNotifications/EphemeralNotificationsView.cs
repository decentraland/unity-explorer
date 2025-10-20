using DCL.Profiles;
using MVC;
using SuperScrollView;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI.EphemeralNotifications
{
    public class EphemeralNotificationsView : ViewBase, IView
    {
        internal struct NotificationData
        {
            public string NotificationTypeName;
            public string[] TextValues;
            public Profile Sender;
        }

        [SerializeField]
        private CanvasGroup mainCanvasGroup;

        [SerializeField]
        private RectTransform notificationsContainer;

        [SerializeField]
        private ScrollView scrollView;

        [SerializeField]
        private LoopListView2 loopListView;

        [SerializeField]
        private int maximumVisibleNotifications;

        private float lastTimeNotificationAdded;

        private List<NotificationData> notifications = new List<NotificationData>(5);

        private void Awake()
        {
            loopListView.InitListView(0, OnGetItemByIndex);
        }

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= notifications.Count)
                return null;

            NotificationData model = notifications[index];
            LoopListViewItem2 newItem = listView.NewListViewItem(model.NotificationTypeName);

            AbstractEphemeralNotification notification = newItem.GetComponent<AbstractEphemeralNotification>();

            notification.SetData(model.Sender, model.TextValues);

            return newItem;
        }

        internal void AddNotification(NotificationData notificationData)
        {
            if (notifications.Count == maximumVisibleNotifications)
                notifications.RemoveAt(notifications.Count - 1);

            notifications.Insert( 0, notificationData);
            loopListView.SetListItemCount(0);
            loopListView.SetListItemCount(notifications.Count);
        }

    }
}
