using DCL.Profiles;
using DG.Tweening;
using MVC;
using SuperScrollView;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        private ScrollRect scrollView;

        [SerializeField]
        private LoopListView2 loopListView;

        [SerializeField]
        private int maximumVisibleNotifications;

        private Queue<NotificationData> pendingNotifications = new Queue<NotificationData>();

        private float lastTimeNotificationAdded;

        private List<NotificationData> notifications = new List<NotificationData>(6);

        private void Awake()
        {
            loopListView.InitListView(0, OnGetItemByIndex);
        }

        private void Start()
        {
            //StartCoroutine(FadeOutAfterTime());
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
            pendingNotifications.Enqueue(notificationData);

            AnimateNewNotification();
        }

        private void Update()
        {
            if (mainCanvasGroup.alpha > 0.999f && Time.time - lastTimeNotificationAdded > 5.0f)
            {
                lastTimeNotificationAdded = float.MinValue;

                mainCanvasGroup.DOFade(0.0f, 3.0f).SetEase(Ease.InOutQuad);
            }
        }

        private void AnimateNewNotification()
        {
            if(pendingNotifications.Count == 0)
                return;

            if (mainCanvasGroup.alpha < 1.0f)
            {
                mainCanvasGroup.DOKill();
                mainCanvasGroup.DOFade(1.0f, 0.5f).SetEase(Ease.InOutQuad);
            }

            lastTimeNotificationAdded = Time.time;

            if (notifications.Count == maximumVisibleNotifications)
                notifications.RemoveAt(notifications.Count - 1);

            notifications.Insert( 0, pendingNotifications.Dequeue());

            loopListView.SetListItemCount(0);
            loopListView.SetListItemCount(Mathf.Min(notifications.Count, maximumVisibleNotifications));

            Vector3 originalPos = scrollView.viewport.localPosition;
            scrollView.viewport.localPosition -= new Vector3(0.0f, 36.0f, 0.0f);

            scrollView.viewport.DOLocalMove(originalPos, 1.0f).OnComplete(AnimateNewNotification);
        }
    }
}
