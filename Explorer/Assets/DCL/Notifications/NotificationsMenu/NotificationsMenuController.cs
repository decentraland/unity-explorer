using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Diagnostics;
using DCL.Notifications.NotificationEntry;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI.SharedSpaceManager;
using DCL.UI.Utilities;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Notifications.NotificationsMenu
{
    public class NotificationsMenuController : IDisposable, IPanelInSharedSpace<ControllerNoData>
    {
        private const int PIXELS_PER_UNIT = 50;
        private const int IDENTITY_CHANGE_POLLING_INTERVAL = 5000;
        private const int DEFAULT_NOTIFICATION_INDEX = 0;
        private const int FRIENDS_NOTIFICATION_INDEX = 1;

        private static readonly List<NotificationType> NOTIFICATION_TYPES_TO_IGNORE = new ()
        {
            NotificationType.INTERNAL_ARRIVED_TO_DESTINATION
        };

        private readonly NotificationsMenuView view;
        private readonly NotificationsRequestController notificationsRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationIconTypes notificationIconTypes;
        private readonly IWebRequestController webRequestController;
        private readonly NftTypeIconSO rarityBackgroundMapping;
        private readonly Dictionary<string, Sprite> notificationThumbnailCache = new ();
        private readonly List<INotification> notifications = new ();
        private readonly CancellationTokenSource lifeCycleCts = new ();
        private readonly IWeb3IdentityCache web3IdentityCache;

        private CancellationTokenSource? notificationThumbnailCts;
        private CancellationTokenSource? notificationPanelCts = new ();
        private int unreadNotifications;
        private Web3Address? previousWeb3Identity;

        public bool IsVisibleInSharedSpace => view.gameObject.activeSelf;

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public NotificationsMenuController(
            NotificationsMenuView view,
            NotificationsRequestController notificationsRequestController,
            INotificationsBusController notificationsBusController,
            NotificationIconTypes notificationIconTypes,
            IWebRequestController webRequestController,
            NftTypeIconSO rarityBackgroundMapping,
            IWeb3IdentityCache web3IdentityCache)
        {
            notificationThumbnailCts = new CancellationTokenSource();

            this.view = view;
            this.notificationsRequestController = notificationsRequestController;
            this.notificationsBusController = notificationsBusController;
            this.notificationIconTypes = notificationIconTypes;
            this.webRequestController = webRequestController;
            this.rarityBackgroundMapping = rarityBackgroundMapping;
            this.web3IdentityCache = web3IdentityCache;
            this.view.OnViewShown += OnViewShown;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            this.view.CloseButton.onClick.AddListener(ClosePanel);
            this.previousWeb3Identity = web3IdentityCache.Identity?.Address;
            CheckIdentityChangeAsync(lifeCycleCts.Token).Forget();
            notificationsBusController.SubscribeToAllNotificationTypesReceived(OnNotificationReceived);
            this.view.LoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void Dispose()
        {
            notificationThumbnailCts.SafeCancelAndDispose();
            notificationPanelCts.SafeCancelAndDispose();
            lifeCycleCts.SafeCancelAndDispose();
            this.view.OnViewShown -= OnViewShown;
            this.view.CloseButton.onClick.RemoveListener(ClosePanel);
        }

        public async UniTask OnShownInSharedSpaceAsync(CancellationToken ct, ControllerNoData parameters)
        {
            notificationPanelCts = notificationPanelCts.SafeRestart();
            await view.ShowAsync(notificationPanelCts.Token);
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntilCanceled(notificationPanelCts.Token);
            await view.HideAsync(notificationPanelCts.Token);
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            notificationPanelCts = notificationPanelCts.SafeRestart();

            await UniTask.WaitUntil(() => !view.gameObject.activeSelf, PlayerLoopTiming.Update, ct);
        }

        private void ClosePanel()
        {
            notificationPanelCts = notificationPanelCts.SafeRestart();
        }

        private void OnViewShown()
        {
            if (unreadNotifications > 0)
            {
                view.LoopList.DoActionForEachShownItem((item2, _) =>
                {
                    INotificationView notificationView = item2!.GetComponent<INotificationView>();
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
            INotification notificationData = notifications[index];
            LoopListViewItem2 listItem;
            INotificationView notificationView;

            switch (notificationData.Type)
            {
                case NotificationType.SOCIAL_SERVICE_FRIENDSHIP_REQUEST:
                case NotificationType.SOCIAL_SERVICE_FRIENDSHIP_ACCEPTED:
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[FRIENDS_NOTIFICATION_INDEX].mItemPrefab.name);
                    notificationView = listItem!.GetComponent<FriendsNotificationView>();
                    break;
                default:
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[DEFAULT_NOTIFICATION_INDEX].mItemPrefab.name);
                    notificationView = listItem!.GetComponent<NotificationView>();
                    break;
            }

            SetItemData(notificationView, notificationData);

            ManageNotificationReadStatus(notificationData, view.gameObject.activeSelf);
            UpdateUnreadNotificationRender();

            notificationView.NotificationImage.SetImage(null);
            if (notificationThumbnailCache.TryGetValue(notificationData.Id, out Sprite thumbnailSprite))
                notificationView.NotificationImage.SetImage(thumbnailSprite);
            else
                LoadNotificationThumbnailAsync(notificationView, notificationData, notificationThumbnailCts!.Token).Forget();

            return listItem;
        }

        private void SetItemData(INotificationView notificationView, INotification notificationData)
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
            var iconBackground = notificationIconTypes.GetNotificationIconBackground(notificationData.Type);
            notificationView.NotificationImageBackground.sprite = iconBackground.backgroundSprite;
            notificationView.NotificationImageBackground.color = iconBackground.backgroundColor;

            ProcessCustomMetadata(notificationData, notificationView);

            notificationView.NotificationClicked += ClickedNotification;
        }

        private void ProcessCustomMetadata(INotification notification, INotificationView notificationView)
        {
            switch (notification)
            {
                case RewardAssignedNotification rewardAssignedNotification:
                    NotificationView nView = (NotificationView)notificationView;
                    nView.NotificationImageBackground.sprite = rarityBackgroundMapping.GetTypeImage(rewardAssignedNotification.Metadata.Rarity);
                    break;
                case RewardInProgressNotification rewardInProgress:
                    NotificationView rewardInProgressView = (NotificationView)notificationView;
                    rewardInProgressView.NotificationImageBackground.sprite = rarityBackgroundMapping.GetTypeImage(rewardInProgress.Metadata.Rarity);
                    break;
                case FriendRequestReceivedNotification friendRequestReceivedNotification:
                    FriendsNotificationView friendNotificationView = (FriendsNotificationView)notificationView;
                    friendNotificationView.ConfigureFromReceivedNotificationData(friendRequestReceivedNotification);
                    friendNotificationView.TimeText.gameObject.SetActive(true);
                    break;
                case FriendRequestAcceptedNotification friendRequestAcceptedNotification:
                    FriendsNotificationView friendNotificationView2 = (FriendsNotificationView)notificationView;
                    friendNotificationView2.ConfigureFromAcceptedNotificationData(friendRequestAcceptedNotification);
                    friendNotificationView2.TimeText.gameObject.SetActive(true);
                    break;
            }
        }

        private void ClickedNotification(NotificationType notificationType, INotification notification)
        {
            notificationsBusController.ClickNotification(notificationType, notification);
        }

        private async UniTask LoadNotificationThumbnailAsync(INotificationView notificationImage, INotification notificationData,
            CancellationToken ct)
        {
            IOwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(notificationData.GetThumbnail())),
                new GetTextureArguments(TextureType.Albedo),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                ct,
                ReportCategory.UI);

            Texture2D texture = ownedTexture.Texture;

            Sprite? thumbnailSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);

            //Try add has been added in case it happens that BE returns duplicated notifications id
            //In that case we will just use the same thumbnail for each notification with the same id
            notificationThumbnailCache.TryAdd(notificationData.Id, thumbnailSprite);
            notificationImage.NotificationImage.SetImage(thumbnailSprite);
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
