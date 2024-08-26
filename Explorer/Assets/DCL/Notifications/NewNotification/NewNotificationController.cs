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
        private const float ANIMATION_DURATION = 0.5f;

        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationIconTypes notificationIconTypes;
        private readonly NftTypeIconSO rarityBackgroundMapping;
        private readonly IWebRequestController webRequestController;
        private readonly Queue<INotification> notificationQueue = new ();
        private bool isDisplaying;
        private ImageController thumbnailImageController;
        private CancellationTokenSource cts;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

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
            notificationsBusController.SubscribeToNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, QueueNewNotification);
            notificationsBusController.SubscribeToNotificationTypeReceived(NotificationType.EVENTS_STARTED, QueueNewNotification);
            notificationsBusController.SubscribeToNotificationTypeReceived(NotificationType.INTERNAL_ARRIVED_TO_DESTINATION, QueueNewNotification);
            notificationsBusController.SubscribeToNotificationTypeReceived(NotificationType.BADGE_GRANTED, QueueNewNotification);
            cts = new CancellationTokenSource();
            cts.Token.ThrowIfCancellationRequested();
        }

        protected override void OnViewInstantiated()
        {
            thumbnailImageController = new ImageController(viewInstance.NotificationView.NotificationImage, webRequestController);
            viewInstance.NotificationView.NotificationClicked += ClickedNotification;
            viewInstance.NotificationView.CloseButton.onClick.AddListener(StopAnimation);
            viewInstance.SystemNotificationView.CloseButton.onClick.AddListener(StopAnimation);
        }

        private void StopAnimation()
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            cts.Token.ThrowIfCancellationRequested();
        }

        private void ClickedNotification(NotificationType notificationType, string _)
        {
            StopAnimation();
            notificationsBusController.ClickNotification(notificationType);
        }

        private void QueueNewNotification(INotification newNotification)
        {
            notificationQueue.Enqueue(newNotification);

            if (!isDisplaying) { DisplayNewNotificationAsync().Forget(); }
        }

        private async UniTaskVoid DisplayNewNotificationAsync()
        {
            while (notificationQueue.Count > 0)
            {
                isDisplaying = true;
                INotification notification = notificationQueue.Dequeue();

                switch (notification.Type)
                {
                    case NotificationType.INTERNAL_ARRIVED_TO_DESTINATION:
                        await ProcessArrivedNotificationAsync(notification);
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
            viewInstance.SystemNotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.SystemNotificationView.NotificationType = notification.Type;
            viewInstance.SystemNotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);

            await AnimateNotificationCanvasGroupAsync(viewInstance.SystemNotificationViewCanvasGroup);
        }

        private async UniTask ProcessDefaultNotificationAsync(INotification notification)
        {
            viewInstance.NotificationView.HeaderText.text = notification.GetHeader();
            viewInstance.NotificationView.TitleText.text = notification.GetTitle();
            viewInstance.NotificationView.NotificationType = notification.Type;
            ProcessCustomMetadata(notification);
            if(!string.IsNullOrEmpty(notification.GetThumbnail()))
                thumbnailImageController.RequestImage(notification.GetThumbnail(), true);

            viewInstance.NotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);

            await AnimateNotificationCanvasGroupAsync(viewInstance.NotificationViewCanvasGroup);
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
                await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: cts.Token);
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

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
