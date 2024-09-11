using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
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
        private static readonly int SHOW_BADGE_TRIGGER = Animator.StringToHash("Show");
        private static readonly int HIDE_BADGE_TRIGGER = Animator.StringToHash("Hide");
        private const float ANIMATION_DURATION = 0.5f;
        private const float TIME_BEFORE_HIDE_NOTIFICATION = 5f;

        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationIconTypes notificationIconTypes;
        private readonly NftTypeIconSO rarityBackgroundMapping;
        private readonly IWebRequestController webRequestController;
        private readonly Queue<INotification> notificationQueue = new ();
        private bool isDisplaying;
        private ImageController thumbnailImageController;
        private ImageController badgeThumbnailImageController;
        private CancellationTokenSource cts;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public NewNotificationController(
            ViewFactoryMethod viewFactory,
            INotificationsBusController notificationsBusController,
            NotificationIconTypes notificationIconTypes,
            NftTypeIconSO rarityBackgroundMapping,
            IWebRequestController webRequestController
        ) : base(viewFactory)
        {
            this.notificationsBusController = notificationsBusController;
            this.notificationIconTypes = notificationIconTypes;
            this.rarityBackgroundMapping = rarityBackgroundMapping;
            this.webRequestController = webRequestController;
            notificationsBusController.SubscribeToAllNotificationTypesReceived(QueueNewNotification);
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
            notificationsBusController.ClickNotification(notificationType, notification);
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
                        await ProcessArrivedNotificationAsync(notification);
                        break;
                    case NotificationType.BADGE_GRANTED:
                        await ProcessBadgeNotificationAsync(notification);
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
            if(!string.IsNullOrEmpty(notification.GetThumbnail()))
                thumbnailImageController.RequestImage(notification.GetThumbnail(), true);

            viewInstance.NotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);

            await AnimateNotificationCanvasGroupAsync(viewInstance.NotificationViewCanvasGroup);
        }

        private async UniTask ProcessBadgeNotificationAsync(INotification notification)
        {
            viewInstance!.BadgeNotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.BadgeNotificationView.TitleText.text = notification.GetTitle();
            viewInstance.BadgeNotificationView.NotificationType = notification.Type;
            viewInstance.BadgeNotificationView.Notification = notification;

            if(!string.IsNullOrEmpty(notification.GetThumbnail()))
                badgeThumbnailImageController.RequestImage(notification.GetThumbnail(), true, true);

            await AnimateBadgeNotificationAsync();
        }

        private void ProcessCustomMetadata(INotification notification)
        {
            switch (notification)
            {
                case RewardAssignedNotification rewardAssignedNotification:
                    viewInstance.NotificationView.NotificationImageBackground.sprite = rarityBackgroundMapping.GetTypeImage(rewardAssignedNotification.Metadata.Rarity);
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
                await UniTask.Delay(TimeSpan.FromSeconds(TIME_BEFORE_HIDE_NOTIFICATION), cancellationToken: cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
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
                viewInstance.BadgeNotificationAnimator.SetTrigger(SHOW_BADGE_TRIGGER);
                await UniTask.Delay(TimeSpan.FromSeconds(TIME_BEFORE_HIDE_NOTIFICATION), cancellationToken: cts.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                viewInstance.BadgeNotificationAnimator.SetTrigger(HIDE_BADGE_TRIGGER);
            }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
