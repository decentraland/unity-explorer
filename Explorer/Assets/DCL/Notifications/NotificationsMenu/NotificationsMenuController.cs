using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Notification.NotificationEntry;
using DCL.Notification.NotificationsBus;
using DCL.Utilities;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Utility;

namespace DCL.Notification.NotificationsMenu
{
    public class NotificationsMenuController
    {
        private const int PIXELS_PER_UNIT = 50;

        private readonly NotificationsMenuView view;
        private readonly NotificationsRequestController notificationsRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationIconTypes notificationIconTypes;
        private readonly IWebRequestController webRequestController;
        private readonly Dictionary<string, Sprite> notificationThumbnailCache = new ();
        private readonly List<INotification> notifications = new ();

        public NotificationsMenuController(
            NotificationsMenuView view,
            NotificationsRequestController notificationsRequestController,
            INotificationsBusController notificationsBusController,
            NotificationIconTypes notificationIconTypes,
            IWebRequestController webRequestController)
        {
            this.view = view;
            this.notificationsRequestController = notificationsRequestController;
            this.notificationsBusController = notificationsBusController;
            this.notificationIconTypes = notificationIconTypes;
            this.webRequestController = webRequestController;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            this.view.CloseButton.onClick.AddListener(ClosePanel);

            InitialNotificationRequest().Forget();
        }

        private void ClosePanel()
        {
            view.gameObject.SetActive(false);
        }

        public void ToggleNotificationsPanel()
        {
            view.gameObject.SetActive(!view.gameObject.activeSelf);
        }

        private async UniTaskVoid InitialNotificationRequest()
        {
            List<INotification> requestNotifications = await notificationsRequestController.RequestNotifications();

            foreach (INotification requestNotification in requestNotifications)
                notifications.Add(requestNotification);

            view.LoopList.SetListItemCount(notifications.Count, false);
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            NotificationView notificationView = listItem!.GetComponent<NotificationView>();
            INotification notificationData = notifications[index];

            SetItemData(notificationView, notificationData);

            if (notificationThumbnailCache.TryGetValue(notificationData.Id, out Sprite thumbnailSprite))
            {
                notificationView.NotificationImage.SetImage(thumbnailSprite);
            }
            else
            {
                LoadNotificationThumbnailAsync(notificationView, notificationData).Forget();
            }

            return listItem;
        }

        private void SetItemData(NotificationView notificationView, INotification notificationData)
        {
            notificationView.NotificationClicked -= ClickedNotification;
            notificationView.HeaderText.text = notificationData.GetHeader();
            notificationView.TitleText.text = notificationData.GetTitle();
            notificationView.NotificationType = notificationData.Type;
            notificationView.NotificationId = notificationData.Id;
            notificationView.CloseButton.gameObject.SetActive(false);
            notificationView.UnreadImage.SetActive(!notificationData.Read);
            notificationView.TimeText.text = TimestampUtilities.GetRelativeTime(notificationData.Timestamp);
            notificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notificationData.Type);
            notificationsRequestController.SetNotificationAsRead(notificationData.Id);
            notificationData.Read = true;
            notificationView.NotificationClicked += ClickedNotification;
        }

        private void ClickedNotification(NotificationType notificationType, string notificationId)
        {
            notificationsBusController.ClickNotification(notificationType);
        }

        private async UniTask LoadNotificationThumbnailAsync(NotificationView notificationView, INotification notificationData)
        {
            Texture2D texture = await webRequestController.GetTextureAsync(new CommonArguments(URLAddress.FromString(notificationData.GetThumbnail())), new GetTextureArguments(false), GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), new System.Threading.CancellationToken());
            Sprite thumbnailSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
            notificationThumbnailCache.Add(notificationData.Id, thumbnailSprite);
            notificationView.NotificationImage.SetImage(thumbnailSprite);
        }
    }
}
