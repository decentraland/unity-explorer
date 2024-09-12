using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Diagnostics;
using DCL.Notifications.NotificationEntry;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.SidebarBus;
using DCL.Utilities;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Notifications.NotificationsMenu
{
    public class NotificationsMenuController : IDisposable
    {
        private const int PIXELS_PER_UNIT = 50;

        private readonly NotificationsMenuView view;
        private readonly NotificationsRequestController notificationsRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationIconTypes notificationIconTypes;
        private readonly IWebRequestController webRequestController;
        private readonly NftTypeIconSO rarityBackgroundMapping;
        private readonly ISidebarBus sidebarBus;
        private readonly Dictionary<string, Sprite> notificationThumbnailCache = new ();
        private readonly List<INotification> notifications = new ();
        private readonly CancellationTokenSource lifeCycleCts = new ();

        private CancellationTokenSource? notificationThumbnailCts;
        private CancellationTokenSource? notificationPanelCts = new CancellationTokenSource();

        public NotificationsMenuController(
            NotificationsMenuView view,
            NotificationsRequestController notificationsRequestController,
            INotificationsBusController notificationsBusController,
            NotificationIconTypes notificationIconTypes,
            IWebRequestController webRequestController,
            ISidebarBus sidebarBus,
            NftTypeIconSO rarityBackgroundMapping)
        {
            notificationThumbnailCts = new CancellationTokenSource();

            this.view = view;
            this.notificationsRequestController = notificationsRequestController;
            this.notificationsBusController = notificationsBusController;
            this.notificationIconTypes = notificationIconTypes;
            this.webRequestController = webRequestController;
            this.sidebarBus = sidebarBus;
            this.rarityBackgroundMapping = rarityBackgroundMapping;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            this.view.CloseButton.onClick.AddListener(ClosePanel);
            InitialNotificationRequestAsync(lifeCycleCts.Token).Forget();
            notificationsBusController.SubscribeToAllNotificationTypesReceived(OnNotificationReceived);
        }

        public void Dispose()
        {
            notificationThumbnailCts.SafeCancelAndDispose();
            notificationPanelCts.SafeCancelAndDispose();
            lifeCycleCts.SafeCancelAndDispose();
        }

        private void ClosePanel()
        {
            sidebarBus.UnblockSidebar();
            notificationPanelCts = notificationPanelCts.SafeRestart();
            view.HideAsync(notificationPanelCts.Token).Forget();
        }

        public void ToggleNotificationsPanel(bool forceClose)
        {
            notificationPanelCts = notificationPanelCts.SafeRestart();

            if (!forceClose && !view.gameObject.activeSelf)
            {
                view.ShowAsync(notificationPanelCts.Token).Forget();
            }
            else if (view.gameObject.activeSelf)
            {
                view.HideAsync(notificationPanelCts.Token).Forget();
            }
        }

        private async UniTaskVoid InitialNotificationRequestAsync(CancellationToken ct)
        {
            List<INotification> requestNotifications = await notificationsRequestController.GetMostRecentNotificationsAsync(ct);

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
                notificationView.NotificationImage.SetImage(thumbnailSprite);
            else
            {
                LoadNotificationThumbnailAsync(notificationView, notificationData, notificationThumbnailCts.Token).Forget();
            }

            return listItem;
        }

        private void SetItemData(NotificationView notificationView, INotification notificationData)
        {
            notificationView.NotificationClicked -= ClickedNotification;
            notificationView.HeaderText.text = notificationData.GetHeader();
            notificationView.TitleText.text = notificationData.GetTitle();
            notificationView.NotificationType = notificationData.Type;
            notificationView.Notification = notificationData;
            notificationView.CloseButton.gameObject.SetActive(false);
            notificationView.UnreadImage.SetActive(!notificationData.Read);
            notificationView.TimeText.text = TimestampUtilities.GetRelativeTime(notificationData.Timestamp);
            notificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notificationData.Type);
            notificationsRequestController.SetNotificationAsReadAsync(notificationData.Id, lifeCycleCts.Token).Forget();
            ProcessCustomMetadata(notificationData, notificationView);
            notificationData.Read = true;
            notificationView.NotificationClicked += ClickedNotification;
        }

        private void ProcessCustomMetadata(INotification notification, NotificationView notificationView)
        {
            switch (notification)
            {
                case RewardAssignedNotification rewardAssignedNotification:
                    notificationView.NotificationImageBackground.sprite = rarityBackgroundMapping.GetTypeImage(rewardAssignedNotification.Metadata.Rarity);
                    break;
            }
        }

        private void ClickedNotification(NotificationType notificationType, INotification notification)
        {
            notificationsBusController.ClickNotification(notificationType, notification);
        }

        private async UniTask LoadNotificationThumbnailAsync(NotificationView notificationView, INotification notificationData,
            CancellationToken ct)
        {
            Texture2D texture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(notificationData.GetThumbnail())),
                new GetTextureArguments(false),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                ct,
                ReportCategory.UI);
            Sprite? thumbnailSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
            notificationThumbnailCache.Add(notificationData.Id, thumbnailSprite);
            notificationView.NotificationImage.SetImage(thumbnailSprite);
        }

        private void OnNotificationReceived(INotification notification)
        {
            notifications.Insert(0, notification);
            view.LoopList.SetListItemCount(notifications.Count, false);
            view.LoopList.RefreshAllShownItem();
        }
    }
}
