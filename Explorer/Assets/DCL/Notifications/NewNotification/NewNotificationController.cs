using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Profiles;
using DCL.UI;
using DCL.WebRequests;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Notifications.NewNotification
{
    public class NewNotificationController : ControllerBase<NewNotificationView>
    {
        private static readonly int SHOW_TRIGGER = Animator.StringToHash("Show");
        private static readonly int HIDE_TRIGGER = Animator.StringToHash("Hide");
        private static readonly TimeSpan TIME_BEFORE_HIDE_NOTIFICATION_TIME_SPAN = TimeSpan.FromSeconds(5f);
        private const float ANIMATION_DURATION = 0.5f;

        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationIconTypes notificationIconTypes;
        private readonly NftTypeIconSO rarityBackgroundMapping;
        private readonly IWebRequestController webRequestController;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly IProfileRepository profileRepository;
        private readonly Queue<INotification> notificationQueue = new ();
        private bool isDisplaying;
        private ImageController? thumbnailImageController;
        private ImageController? badgeThumbnailImageController;
        private ImageController? friendsThumbnailImageController;
        private ImageController? marketplaceCreditsThumbnailImageController;
        private CancellationTokenSource animationCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public NewNotificationController(
            ViewFactoryMethod viewFactory,
            INotificationsBusController notificationsBusController,
            NotificationIconTypes notificationIconTypes,
            NftTypeIconSO rarityBackgroundMapping,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache,
            IProfileRepository profileRepository) : base(viewFactory)
        {
            this.notificationsBusController = notificationsBusController;
            this.notificationIconTypes = notificationIconTypes;
            this.rarityBackgroundMapping = rarityBackgroundMapping;
            this.webRequestController = webRequestController;
            this.profileThumbnailCache = profileThumbnailCache;
            this.profileRepository = profileRepository;
            notificationsBusController.SubscribeToAllNotificationTypesReceived(QueueNewNotification);
            animationCts = new CancellationTokenSource();
            animationCts.Token.ThrowIfCancellationRequested();
        }

        protected override void OnViewInstantiated()
        {
            thumbnailImageController = new ImageController(viewInstance!.NotificationView.NotificationImage, webRequestController);
            viewInstance.NotificationView.NotificationClicked += ClickedNotification;
            viewInstance.NotificationView.CloseButton.onClick.AddListener(StopAnimation);
            viewInstance.SystemNotificationView.CloseButton.onClick.AddListener(StopAnimation);
            badgeThumbnailImageController = new ImageController(viewInstance.BadgeNotificationView.NotificationImage, webRequestController);
            viewInstance.BadgeNotificationView.NotificationClicked += ClickedNotification;
            friendsThumbnailImageController = new ImageController(viewInstance.FriendsNotificationView.NotificationImage, webRequestController);
            viewInstance.FriendsNotificationView.NotificationClicked += ClickedNotification;
            marketplaceCreditsThumbnailImageController = new ImageController(viewInstance.MarketplaceCreditsNotificationView.NotificationImage, webRequestController);
            viewInstance.MarketplaceCreditsNotificationView.NotificationClicked += ClickedNotification;
        }

        private void StopAnimation()
        {
            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();
            animationCts.Token.ThrowIfCancellationRequested();
        }

        private void ClickedNotification(NotificationType notificationType, INotification notification)
        {
            StopAnimation();
            notificationsBusController.ClickNotification(notificationType, notification);
        }

        private void QueueNewNotification(INotification newNotification)
        {
            notificationQueue.Enqueue(newNotification);

            if (!isDisplaying) { DisplayNewNotificationAsync(animationCts.Token).Forget(); }
        }

        private async UniTaskVoid DisplayNewNotificationAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return;

            while (notificationQueue.Count > 0)
            {
                isDisplaying = true;
                INotification notification = notificationQueue.Dequeue();

                switch (notification.Type)
                {
                    case NotificationType.INTERNAL_ARRIVED_TO_DESTINATION:
                        await ProcessArrivedNotificationAsync(notification);
                        break;
                    case NotificationType.BADGE_GRANTED:
                        await ProcessBadgeNotificationAsync(notification);
                        break;
                    case NotificationType.SOCIAL_SERVICE_FRIENDSHIP_REQUEST:
                    case NotificationType.SOCIAL_SERVICE_FRIENDSHIP_ACCEPTED:
                        await ProcessFriendsNotificationAsync(notification);
                        break;
                    case NotificationType.CREDITS_GOAL_COMPLETED:
                        await ProcessMarketplaceCreditsNotificationAsync(notification);
                        break;
                    case NotificationType.REFERRAL_INVITED_USERS_ACCEPTED:
                    case NotificationType.REFERRAL_NEW_TIER_REACHED:
                        await ProcessReferralNotificationAsync((ReferralNotification) notification, ct);
                        break;
                    default:
                        await ProcessDefaultNotificationAsync(notification);
                        break;
                }
            }

            isDisplaying = false;
        }

        private async UniTask ProcessArrivedNotificationAsync(INotification notification)
        {
            viewInstance!.SystemNotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.SystemNotificationView.NotificationType = notification.Type;
            viewInstance.SystemNotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);

            await AnimateNotificationCanvasGroupAsync(viewInstance.SystemNotificationViewCanvasGroup);
        }

        private async UniTask ProcessDefaultNotificationAsync(INotification notification)
        {
            viewInstance!.NotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.NotificationView.TitleText.text = notification.GetTitle();
            viewInstance.NotificationView.NotificationType = notification.Type;
            viewInstance.NotificationView.Notification = notification;
            ProcessCustomMetadata(notification);

            if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                thumbnailImageController.RequestImage(notification.GetThumbnail(), true);

            viewInstance.NotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);

            await AnimateNotificationCanvasGroupAsync(viewInstance.NotificationViewCanvasGroup);
        }

        private async UniTask ProcessFriendsNotificationAsync(INotification notification)
        {
            viewInstance!.FriendsNotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.FriendsNotificationView.NotificationType = notification.Type;
            viewInstance.FriendsNotificationView.Notification = notification;
            ProcessCustomMetadata(notification);

            if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                friendsThumbnailImageController.RequestImage(notification.GetThumbnail(), true);

            viewInstance.FriendsNotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);

            if (notification.Type == NotificationType.SOCIAL_SERVICE_FRIENDSHIP_ACCEPTED)
                viewInstance.FriendsNotificationView.PlayAcceptedNotificationAudio();
            else
                viewInstance.FriendsNotificationView.PlayRequestNotificationAudio();

            await AnimateNotificationCanvasGroupAsync(viewInstance.FriendsNotificationViewCanvasGroup);
        }

        private async UniTask ProcessBadgeNotificationAsync(INotification notification)
        {
            viewInstance!.BadgeNotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.BadgeNotificationView.TitleText.text = notification.GetTitle();
            viewInstance.BadgeNotificationView.NotificationType = notification.Type;
            viewInstance.BadgeNotificationView.Notification = notification;

            if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                badgeThumbnailImageController.RequestImage(notification.GetThumbnail(), true, true);

            await AnimateBadgeNotificationAsync();
        }

        private async UniTask ProcessReferralNotificationAsync(ReferralNotification notification, CancellationToken ct)
        {
            viewInstance!.NotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.NotificationView.TitleText.text = notification.GetTitle();
            viewInstance.NotificationView.NotificationType = notification.Type;
            viewInstance.NotificationView.Notification = notification;

            if (!string.IsNullOrEmpty(notification.Metadata.invitedUserAddress))
                await DownloadProfileThumbnailAsync(notification.Metadata.invitedUserAddress);
            else if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                thumbnailImageController!.RequestImage(notification.GetThumbnail(), true);

            viewInstance.NotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);

            await AnimateNotificationCanvasGroupAsync(viewInstance.NotificationViewCanvasGroup);

            return;

            async UniTask DownloadProfileThumbnailAsync(string user)
            {
                thumbnailImageController!.StartLoading();

                Profile? profile = await profileRepository.GetAsync(user, ct);

                if (profile != null)
                {
                    Sprite? sprite = await profileThumbnailCache.GetThumbnailAsync(user, profile.Avatar.FaceSnapshotUrl, ct);

                    if (sprite != null)
                        thumbnailImageController!.SetImage(sprite);
                }

                thumbnailImageController.StopLoading();
            }
        }

        private async UniTask ProcessMarketplaceCreditsNotificationAsync(INotification notification)
        {
            viewInstance!.MarketplaceCreditsNotificationView.SetHeaderText(notification.GetHeader());
            viewInstance.MarketplaceCreditsNotificationView.SetTitleText(notification.GetTitle());
            viewInstance.MarketplaceCreditsNotificationView.SetNotification(notification.Type, notification);

            if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                marketplaceCreditsThumbnailImageController.RequestImage(notification.GetThumbnail(), true, true);

            await AnimateMarketplaceCreditsNotificationAsync();
        }

        private void ProcessCustomMetadata(INotification notification)
        {
            switch (notification)
            {
                case RewardAssignedNotification rewardAssignedNotification:
                    viewInstance!.NotificationView.NotificationImageBackground.sprite = rarityBackgroundMapping.GetTypeImage(rewardAssignedNotification.Metadata.Rarity);
                    break;
                case FriendRequestAcceptedNotification friendRequestAcceptedNotification:
                    viewInstance!.FriendsNotificationView.ConfigureFromAcceptedNotificationData(friendRequestAcceptedNotification);
                    break;
                case FriendRequestReceivedNotification friendRequestReceivedNotification:
                    viewInstance!.FriendsNotificationView.ConfigureFromReceivedNotificationData(friendRequestReceivedNotification);
                    break;
            }
        }

        private async UniTask AnimateNotificationCanvasGroupAsync(CanvasGroup notificationCanvasGroup)
        {
            try
            {
                notificationCanvasGroup.interactable = true;
                notificationCanvasGroup.blocksRaycasts = true;
                await notificationCanvasGroup.DOFade(1, ANIMATION_DURATION).ToUniTask(cancellationToken: animationCts.Token);
                await UniTask.Delay(TIME_BEFORE_HIDE_NOTIFICATION_TIME_SPAN, cancellationToken: animationCts.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                notificationCanvasGroup.interactable = false;
                notificationCanvasGroup.blocksRaycasts = false;
                await notificationCanvasGroup.DOFade(0, ANIMATION_DURATION).ToUniTask();
            }
        }

        private async UniTask AnimateBadgeNotificationAsync()
        {
            if (viewInstance == null)
                return;

            try
            {
                viewInstance.BadgeNotificationView.PlayNotificationAudio();
                viewInstance.BadgeNotificationAnimator.SetTrigger(SHOW_TRIGGER);
                await UniTask.Delay(TIME_BEFORE_HIDE_NOTIFICATION_TIME_SPAN, cancellationToken: animationCts.Token);
            }
            catch (OperationCanceledException) { }
            finally { viewInstance.BadgeNotificationAnimator.SetTrigger(HIDE_TRIGGER); }
        }

        private async UniTask AnimateMarketplaceCreditsNotificationAsync()
        {
            if (viewInstance == null)
                return;

            try
            {
                viewInstance.MarketplaceCreditsNotificationView.PlayNotificationAudio();
                viewInstance.MarketplaceCreditsNotificationAnimator.SetTrigger(SHOW_TRIGGER);
                await UniTask.Delay(TIME_BEFORE_HIDE_NOTIFICATION_TIME_SPAN, cancellationToken: animationCts.Token);
            }
            catch (OperationCanceledException) { }
            finally { viewInstance.MarketplaceCreditsNotificationAnimator.SetTrigger(HIDE_TRIGGER); }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
