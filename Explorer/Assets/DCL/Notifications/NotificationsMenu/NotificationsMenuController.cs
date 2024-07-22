using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Notification.NotificationEntry;
using DCL.Notification.NotificationsBus;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;
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
        private readonly Dictionary<string, (INotification, NotificationView)> notificationsReferenceCache = new ();
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

            InitialNotificationRequest().Forget();
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

            if (!notificationsReferenceCache.TryAdd(notificationData.Id, (notificationData, notificationView)))
            {
                notificationsReferenceCache[notificationData.Id] = (notificationData, notificationView);
            }

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
            notificationView.HeaderText.text = notificationData.GetHeader();
            notificationView.TitleText.text = notificationData.GetTitle();
            notificationView.NotificationType = notificationData.Type;
            notificationView.NotificationId = notificationData.Id;
            notificationView.CloseButton.gameObject.SetActive(false);
            notificationView.UnreadImage.SetActive(!notificationData.Read);
            notificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notificationData.Type);
            notificationView.NotificationClicked -= OnNotificationClicked;
            notificationView.NotificationClicked += OnNotificationClicked;
        }

        private void OnNotificationClicked(NotificationType notificationType, string notificationId)
        {
            if (notificationsReferenceCache.TryGetValue(notificationId, out (INotification, NotificationView) notificationReference))
            {
                if (notificationReference.Item1.Read)
                    return;

                notificationReference.Item1.Read = true;
                notificationReference.Item2.UnreadImage.gameObject.SetActive(false);
                notificationsRequestController.SetNotificationAsRead(notificationId);
            }
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
