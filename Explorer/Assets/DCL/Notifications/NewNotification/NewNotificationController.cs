using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.FeatureFlags;
using DCL.UI;
using DCL.WebRequests;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Notifications.NewNotification
{
    public class NewNotificationController : ControllerBase<NewNotificationView>
    {
        private const float ANIMATION_DURATION = 0.5f;
        private static readonly int SHOW_TRIGGER = Animator.StringToHash("Show");
        private static readonly int HIDE_TRIGGER = Animator.StringToHash("Hide");
        private static readonly TimeSpan TIME_BEFORE_HIDE_NOTIFICATION_TIME_SPAN = TimeSpan.FromSeconds(5f);

        private readonly NotificationIconTypes notificationIconTypes;
        private readonly NotificationDefaultThumbnails notificationDefaultThumbnails;
        private readonly NftTypeIconSO rarityBackgroundMapping;
        private readonly IWebRequestController webRequestController;
        private readonly Queue<INotification> notificationQueue = new ();
        private bool isDisplaying;
        private ImageController thumbnailImageController;
        private ImageController badgeThumbnailImageController;
        private ImageController friendsThumbnailImageController;
        private ImageController marketplaceCreditsThumbnailImageController;
        private ImageController communityThumbnailImageController;
        private CancellationTokenSource cts;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public NewNotificationController(
            ViewFactoryMethod viewFactory,
            NotificationIconTypes notificationIconTypes,
            NotificationDefaultThumbnails notificationDefaultThumbnails,
            NftTypeIconSO rarityBackgroundMapping,
            IWebRequestController webRequestController) : base(viewFactory)
        {
            this.notificationIconTypes = notificationIconTypes;
            this.notificationDefaultThumbnails = notificationDefaultThumbnails;
            this.rarityBackgroundMapping = rarityBackgroundMapping;
            this.webRequestController = webRequestController;
            NotificationsBusController.NotificationsBus.NotificationsBusController.Instance.SubscribeToAllNotificationTypesReceived(QueueNewNotification);
            cts = new CancellationTokenSource();
            cts.Token.ThrowIfCancellationRequested();
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

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
            {
                communityThumbnailImageController = new ImageController(viewInstance.CommunityVoiceChatNotificationView.NotificationImage, webRequestController);
                viewInstance.CommunityVoiceChatNotificationView.NotificationClicked += ClickedNotification;
            }
        }

        private void StopAnimation()
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            cts.Token.ThrowIfCancellationRequested();
        }

        private void ClickedNotification(NotificationType notificationType, INotification notification)
        {
            StopAnimation();
            NotificationsBusController.NotificationsBus.NotificationsBusController.Instance.ClickNotification(notificationType, notification);
        }

        private void QueueNewNotification(INotification newNotification)
        {
            notificationQueue.Enqueue(newNotification);

            if (!isDisplaying) { DisplayNewNotificationAsync().Forget(); }
        }

        private async UniTaskVoid DisplayNewNotificationAsync()
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
                    case NotificationType.INTERNAL_SERVER_ERROR:
                        await ProcessArrivedNotificationAsync(notification);
                        break;
                    case NotificationType.COMMUNITY_VOICE_CHAT_STARTED:
                        if (FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
                            await ProcessCommunityVoiceChatStartedNotificationAsync(notification);

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
                    case NotificationType.INTERNAL_DEFAULT_SUCCESS:
                        await ProcessArrivedNotificationAsync(notification, false);
                        break;
                    default:
                        await ProcessDefaultNotificationAsync(notification);
                        break;
                }
            }

            isDisplaying = false;
        }

        private async UniTask ProcessCommunityVoiceChatStartedNotificationAsync(INotification notification)
        {
            viewInstance!.CommunityVoiceChatNotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.CommunityVoiceChatNotificationView.TitleText.text = notification.GetTitle();
            viewInstance.CommunityVoiceChatNotificationView.NotificationType = notification.Type;
            viewInstance.CommunityVoiceChatNotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);
            viewInstance.CommunityVoiceChatNotificationView.Notification = notification;

            if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                communityThumbnailImageController.RequestImage(notification.GetThumbnail(), true);

            await AnimateNotificationCanvasGroupAsync(viewInstance.CommunityNotificationCanvasGroup);
        }

        private async UniTask ProcessArrivedNotificationAsync(INotification notification, bool enableCloseButton = true)
        {
            viewInstance!.SystemNotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.SystemNotificationView.NotificationType = notification.Type;
            viewInstance.SystemNotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);
            viewInstance!.SystemNotificationView.CloseButtonContainer.SetActive(enableCloseButton);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)viewInstance.transform);

            await AnimateNotificationCanvasGroupAsync(viewInstance.SystemNotificationViewCanvasGroup);
        }

        private async UniTask ProcessDefaultNotificationAsync(INotification notification)
        {
            viewInstance!.NotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.NotificationView.TitleText.text = notification.GetTitle();
            viewInstance.NotificationView.NotificationType = notification.Type;
            viewInstance.NotificationView.Notification = notification;
            ProcessCustomMetadata(notification);

            DefaultNotificationThumbnail defaultThumbnail = notificationDefaultThumbnails.GetNotificationDefaultThumbnail(notification.Type);

            if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                thumbnailImageController.RequestImage(notification.GetThumbnail(), true, fitAndCenterImage: defaultThumbnail.FitAndCenter, defaultSprite: defaultThumbnail.Thumbnail);
            else
                thumbnailImageController.SetImage(defaultThumbnail.Thumbnail, defaultThumbnail.FitAndCenter);

            viewInstance.NotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);

            await AnimateNotificationCanvasGroupAsync(viewInstance.NotificationViewCanvasGroup);
        }

        private async UniTask ProcessFriendsNotificationAsync(INotification notification)
        {
            viewInstance!.FriendsNotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.FriendsNotificationView.NotificationType = notification.Type;
            viewInstance.FriendsNotificationView.Notification = notification;
            ProcessCustomMetadata(notification);

            DefaultNotificationThumbnail defaultThumbnail = notificationDefaultThumbnails.GetNotificationDefaultThumbnail(notification.Type);

            if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                friendsThumbnailImageController.RequestImage(notification.GetThumbnail(), true, fitAndCenterImage: defaultThumbnail.FitAndCenter, defaultSprite: defaultThumbnail.Thumbnail);
            else
                friendsThumbnailImageController.SetImage(defaultThumbnail.Thumbnail, defaultThumbnail.FitAndCenter);

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

            DefaultNotificationThumbnail defaultThumbnail = notificationDefaultThumbnails.GetNotificationDefaultThumbnail(notification.Type);

            if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                badgeThumbnailImageController.RequestImage(notification.GetThumbnail(), true, true, fitAndCenterImage: defaultThumbnail.FitAndCenter, defaultSprite: defaultThumbnail.Thumbnail);
            else
                badgeThumbnailImageController.SetImage(defaultThumbnail.Thumbnail, defaultThumbnail.FitAndCenter);

            await AnimateBadgeNotificationAsync();
        }

        private async UniTask ProcessMarketplaceCreditsNotificationAsync(INotification notification)
        {
            viewInstance!.MarketplaceCreditsNotificationView.SetHeaderText(notification.GetHeader());
            viewInstance.MarketplaceCreditsNotificationView.SetTitleText(notification.GetTitle());
            viewInstance.MarketplaceCreditsNotificationView.SetNotification(notification.Type, notification);

            DefaultNotificationThumbnail defaultThumbnail = notificationDefaultThumbnails.GetNotificationDefaultThumbnail(notification.Type);

            if (!string.IsNullOrEmpty(notification.GetThumbnail()))
                marketplaceCreditsThumbnailImageController.RequestImage(notification.GetThumbnail(), true, true, fitAndCenterImage: defaultThumbnail.FitAndCenter, defaultSprite: defaultThumbnail.Thumbnail);
            else
                marketplaceCreditsThumbnailImageController.SetImage(defaultThumbnail.Thumbnail, defaultThumbnail.FitAndCenter);

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
                await notificationCanvasGroup.DOFade(1, ANIMATION_DURATION).ToUniTask(cancellationToken: cts.Token);
                await UniTask.Delay(TIME_BEFORE_HIDE_NOTIFICATION_TIME_SPAN, cancellationToken: cts.Token);
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
                await UniTask.Delay(TIME_BEFORE_HIDE_NOTIFICATION_TIME_SPAN, cancellationToken: cts.Token);
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
                await UniTask.Delay(TIME_BEFORE_HIDE_NOTIFICATION_TIME_SPAN, cancellationToken: cts.Token);
            }
            catch (OperationCanceledException) { }
            finally { viewInstance.MarketplaceCreditsNotificationAnimator.SetTrigger(HIDE_TRIGGER); }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
