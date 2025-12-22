using DCL.Profiles;
using DG.Tweening;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.EphemeralNotifications
{
    public class EphemeralNotificationsView : ViewBase, IView
    {
        /// <summary>
        ///     Stores info related to a notification which may not be immediately processed.
        /// </summary>
        internal struct NotificationData
        {
            /// <summary>
            ///     The type matches the name of the prefab to be used in the list.
            /// </summary>
            public string NotificationTypeName;

            /// <summary>
            ///     Text values that will be used to compose the label of the notification in the UI.
            /// </summary>
            public string[] TextValues;

            /// <summary>
            ///     Profile data of the user that sent the notification.
            /// </summary>
            public Profile Sender;
        }

        [SerializeField]
        private CanvasGroup mainCanvasGroup;

        [SerializeField]
        private RectTransform notificationsContainer;

        [SerializeField]
        private ScrollRect scrollView;

        [SerializeField]
        private LoopListView2 loopListView;

        [SerializeField]
        private int maximumVisibleNotifications;

        [Tooltip("The amount of time the appearance animation takes, in seconds.")]
        [SerializeField]
        private float fadeInAnimationDuration = 0.5f;

        [Tooltip("The amount of time the notification stays fully visible, in seconds.")]
        [SerializeField]
        private float notificationDuration = 3.0f;

        [Tooltip("The amount of time the hiding animation takes, in seconds.")]
        [SerializeField]
        private float fadeOutAnimationDuration = 0.5f;

        private readonly Queue<NotificationData> pendingNotifications = new ();
        private List<NotificationData> notifications;

        private float[] notificationStartTimes;

        private void Awake()
        {
            loopListView.InitListView(0, OnGetItemByIndex);
            notificationStartTimes = new float[maximumVisibleNotifications];
            notifications = new List<NotificationData>(maximumVisibleNotifications);

            for (int i = 0; i < notificationStartTimes.Length; ++i)
            {
                notificationStartTimes[i] = float.MinValue;
            }
        }

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= notifications.Count)
                return null;

            var model = notifications[index];
            var newItem = listView.NewListViewItem(model.NotificationTypeName);

            var notification = newItem.GetComponent<AbstractEphemeralNotification>();
            notification.SetOpacity(0.0f);

            notification.SetData(model.Sender, model.TextValues);

            return newItem;
        }

        internal void AddNotification(NotificationData notificationData)
        {
            pendingNotifications.Enqueue(notificationData);
        }

        private void Update()
        {
            AnimateNewNotification();
        }

        private void LateUpdate()
        {
            for (int i = loopListView.ShownItemCount - 1; i >= 0; --i)
            {
                var notification = loopListView.GetShownItemByItemIndex(i).GetComponent<AbstractEphemeralNotification>();
                float elapsedTime = Time.time - notificationStartTimes[i];
                float opacity = elapsedTime < fadeInAnimationDuration ? elapsedTime / fadeInAnimationDuration : elapsedTime < notificationDuration + fadeInAnimationDuration ? 1.0f : 1.0f - (elapsedTime - notificationDuration - fadeInAnimationDuration) / fadeOutAnimationDuration;
                notification.SetOpacity(opacity);
            }
        }

        private void AnimateNewNotification()
        {
            if (pendingNotifications.Count == 0)
                return;

            // When several notifications arrive at the same time, it adds a delay to each of them so the user can read them all
            if (Time.time - notificationStartTimes[0] < 1.0f)
                return;

            // Oldest notification is removed when maximum is reached
            if (notifications.Count == maximumVisibleNotifications)
                notifications.RemoveAt(notifications.Count - 1);

            // Consumes one pending notification
            notifications.Insert( 0, pendingNotifications.Dequeue());

            // Displaces the start time of all notifications, used when updating opacity
            for (int i = notificationStartTimes.Length - 1; i > 0; --i)
                notificationStartTimes[i] = notificationStartTimes[i - 1];

            notificationStartTimes[0] = Time.time;

            // Removes all the notifications whose lifetime has finished
            float totalDuration = fadeInAnimationDuration + notificationDuration + fadeOutAnimationDuration;

            for (int i = notifications.Count - 1; i >= 0; --i)
            {
                if (Time.time - notificationStartTimes[i] > totalDuration)
                    notifications.RemoveAt(i);
            }

            // Refreshes UI list
            loopListView.SetListItemCount(0);
            loopListView.SetListItemCount(Mathf.Min(notifications.Count, maximumVisibleNotifications));

            // Fakes toast animation
            var originalPos = scrollView.viewport.localPosition;
            scrollView.viewport.localPosition -= new Vector3(0.0f, 36.0f, 0.0f);

            scrollView.viewport.DOLocalMove(originalPos, fadeInAnimationDuration).OnComplete(AnimateNewNotification);
        }
    }
}