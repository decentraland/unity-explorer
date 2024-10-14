using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Diagnostics;
using DCL.Notifications.NotificationEntry;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.SidebarBus;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
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
        private const int IDENTITY_CHANGE_POLLING_INTERVAL = 5000;
        private static readonly List<NotificationType> NOTIFICATION_TYPES_TO_IGNORE = new ()
        {
            NotificationType.INTERNAL_ARRIVED_TO_DESTINATION
        };

        private readonly NotificationsMenuView view;
        private readonly NotificationsRequestController notificationsRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationIconTypes notificationIconTypes;
        private readonly IWebRequestController webRequestController;
        private readonly IGetTextureArgsFactory getTextureArgsFactory;
        private readonly NftTypeIconSO rarityBackgroundMapping;
        private readonly ISidebarBus sidebarBus;
        private readonly Dictionary<string, Sprite> notificationThumbnailCache = new ();
        private readonly List<INotification> notifications = new ();
        private readonly CancellationTokenSource lifeCycleCts = new ();
        private readonly IWeb3IdentityCache web3IdentityCache;

        private CancellationTokenSource? notificationThumbnailCts;
        private CancellationTokenSource? notificationPanelCts = new CancellationTokenSource();
        private int unreadNotifications;
        private Web3Address? previousWeb3Identity;

        public NotificationsMenuController(
            NotificationsMenuView view,
            NotificationsRequestController notificationsRequestController,
            INotificationsBusController notificationsBusController,
            NotificationIconTypes notificationIconTypes,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory,
            ISidebarBus sidebarBus,
            NftTypeIconSO rarityBackgroundMapping,
            IWeb3IdentityCache web3IdentityCache)
        {
            notificationThumbnailCts = new CancellationTokenSource();

            this.view = view;
            this.notificationsRequestController = notificationsRequestController;
            this.notificationsBusController = notificationsBusController;
            this.notificationIconTypes = notificationIconTypes;
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
            this.sidebarBus = sidebarBus;
            this.rarityBackgroundMapping = rarityBackgroundMapping;
            this.web3IdentityCache = web3IdentityCache;
            this.view.OnViewShown += OnViewShown;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            this.view.CloseButton.onClick.AddListener(ClosePanel);
            this.previousWeb3Identity = web3IdentityCache.Identity?.Address;
            CheckIdentityChangeAsync(lifeCycleCts.Token).Forget();
            notificationsBusController.SubscribeToAllNotificationTypesReceived(OnNotificationReceived);
        }

        public void Dispose()
        {
            notificationThumbnailCts.SafeCancelAndDispose();
            notificationPanelCts.SafeCancelAndDispose();
            lifeCycleCts.SafeCancelAndDispose();
            this.view.OnViewShown -= OnViewShown;
            this.view.CloseButton.onClick.RemoveListener(ClosePanel);
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

            if (!forceClose && !view.gameObject.activeSelf) { view.ShowAsync(notificationPanelCts.Token).Forget(); }
            else if (view.gameObject.activeSelf) { view.HideAsync(notificationPanelCts.Token).Forget(); }
        }

        private void OnViewShown()
        {
            if (unreadNotifications > 0)
            {
                view.LoopList.DoActionForEachShownItem((item2, param) =>
                {
                    NotificationView notificationView = item2!.GetComponent<NotificationView>();
                    INotification notificationData = notificationView.Notification;

                    ManageNotificationReadStatus(notificationData, true);

                    notificationView.UnreadImage.SetActive(false);
                }, null);

                UpdateUnreadNotificationRender();
            }
        }

        private async UniTaskVoid CheckIdentityChangeAsync(CancellationToken token)
        {
            if (previousWeb3Identity != null)
                await InitialNotificationRequestAsync(token);

            while (token.IsCancellationRequested == false)
            {
                if (previousWeb3Identity != web3IdentityCache.Identity?.Address && web3IdentityCache.Identity?.Address != null)
                {
                    previousWeb3Identity = web3IdentityCache.Identity?.Address;
                    await InitialNotificationRequestAsync(lifeCycleCts.Token);
                }
                else
                    await UniTask.Delay(IDENTITY_CHANGE_POLLING_INTERVAL, cancellationToken: token);
            }
        }

        private async UniTask InitialNotificationRequestAsync(CancellationToken ct)
        {
            unreadNotifications = 0;
            notifications.Clear();
            view.LoopList.SetListItemCount(notifications.Count, false);

            List<INotification> requestNotifications = await notificationsRequestController.GetMostRecentNotificationsAsync(ct);

            foreach (INotification requestNotification in requestNotifications)
                notifications.Add(requestNotification);

            view.LoopList.SetListItemCount(notifications.Count, false);

            foreach (var notification in requestNotifications)
                if (notification.Read == false)
                    unreadNotifications++;

            UpdateUnreadNotificationRender();
        }

        private void UpdateUnreadNotificationRender()
        {
            view.unreadNotificationCounterText.SetText("{0}", unreadNotifications);
            view.notificationIndicator.SetActive(unreadNotifications > 0);
        }

        private void ManageNotificationReadStatus(INotification notificationData, bool isViewOpen)
        {
            if (notificationData.Read == false && isViewOpen)
            {
                unreadNotifications--;
                notificationsRequestController.SetNotificationAsReadAsync(notificationData.Id, lifeCycleCts.Token).Forget();
                notificationData.Read = true;
            }
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            NotificationView notificationView = listItem!.GetComponent<NotificationView>();
            INotification notificationData = notifications[index];

            ManageNotificationReadStatus(notificationData, view.gameObject.activeSelf);
            UpdateUnreadNotificationRender();

            SetItemData(notificationView, notificationData);

            if (notificationThumbnailCache.TryGetValue(notificationData.Id, out Sprite thumbnailSprite))
                notificationView.NotificationImage.SetImage(thumbnailSprite);
            else
                LoadNotificationThumbnailAsync(notificationView, notificationData, notificationThumbnailCts.Token).Forget();

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

            ProcessCustomMetadata(notificationData, notificationView);

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
            OwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(notificationData.GetThumbnail())),
                getTextureArgsFactory.NewArguments(false),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                ct,
                ReportCategory.UI);

            Texture2D texture = ownedTexture.Texture;

            Sprite? thumbnailSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);

            //TODO changed to TryAdd, because in some cases it could hit exception if to call Add()
            notificationThumbnailCache.TryAdd(notificationData.Id, thumbnailSprite);
            notificationView.NotificationImage.SetImage(thumbnailSprite);
        }

        private void OnNotificationReceived(INotification notification)
        {
            if (NOTIFICATION_TYPES_TO_IGNORE.Contains(notification.Type))
                return;

            notifications.Insert(0, notification);
            view.LoopList.SetListItemCount(notifications.Count, false);
            view.LoopList.RefreshAllShownItem();
        }
    }
}
