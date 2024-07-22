using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Notification.NotificationEntry;
using DCL.Notification.NotificationsBus;
using DCL.WebRequests;
using SuperScrollView;
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
        private readonly IWebRequestController webRequestController;

        private Dictionary<string, Sprite> notificationThumbnailCache = new ();
        private List<INotification> notifications = new List<INotification>();

        public NotificationsMenuController(
            NotificationsMenuView view,
            NotificationsRequestController notificationsRequestController,
            INotificationsBusController notificationsBusController,
            IWebRequestController webRequestController)
        {
            this.view = view;
            this.notificationsRequestController = notificationsRequestController;
            this.notificationsBusController = notificationsBusController;
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
            List<INotification> RequestNotifications = await notificationsRequestController.RequestNotifications();

            foreach (INotification requestNotification in RequestNotifications)
            {
                notifications.Add(requestNotification);
            }
            view.LoopList.SetListItemCount(notifications.Count, false);
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int arg2)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            NotificationView notificationView = listItem!.GetComponent<NotificationView>();
            INotification notificationData = notifications[arg2];

            notificationView.HeaderText.text = notificationData.GetHeader();
            notificationView.TitleText.text = notificationData.GetTitle();
            notificationView.NotificationType = notificationData.Type;

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

        private async UniTask LoadNotificationThumbnailAsync(NotificationView notificationView, INotification notificationData)
        {
            Texture2D texture = await webRequestController.GetTextureAsync(new CommonArguments(URLAddress.FromString(notificationData.GetThumbnail())), new GetTextureArguments(false), GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), new System.Threading.CancellationToken());
            Sprite thumbnailSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
            notificationThumbnailCache.Add(notificationData.Id, thumbnailSprite);
            notificationView.NotificationImage.SetImage(thumbnailSprite);
        }
    }
}
