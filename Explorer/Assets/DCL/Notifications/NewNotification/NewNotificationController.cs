using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Notification.NotificationsBus;
using DCL.UI;
using DCL.WebRequests;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Notification.NewNotification
{
    public class NewNotificationController : ControllerBase<NewNotificationView>
    {
        private const float ANIMATION_DURATION = 0.5f;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationIconTypes notificationIconTypes;
        private readonly NftTypeIconSO rarityBackgroundMapping;
        private readonly IWebRequestController webRequestController;
        private readonly Queue<INotification> notificationQueue = new ();
        private bool isDisplaying = false;
        private ImageController thumbnailImageController;
        private CancellationTokenSource cts;

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
            notificationsBusController.OnNotificationAdded += QueueNewNotification;
        }

        protected override void OnViewInstantiated()
        {
            thumbnailImageController = new ImageController(viewInstance.NotificationView.NotificationImage, webRequestController);
            viewInstance.NotificationView.OnNotificationClicked += ClickedOnNotification;
            viewInstance.NotificationView.CloseButton.onClick.AddListener(StopAnimation);
        }

        private void StopAnimation()
        {
            cts.SafeCancelAndDispose();
        }

        private void ClickedOnNotification(NotificationType notificationType)
        {
            StopAnimation();
            notificationsBusController.ClickNotification(notificationType);
        }

        private void QueueNewNotification(INotification newNotification)
        {
            notificationQueue.Enqueue(newNotification);
            if (!isDisplaying)
            {
                DisplayNewNotificationAsync().Forget();
            }
        }

        private async UniTaskVoid DisplayNewNotificationAsync()
        {
            while (notificationQueue.Count > 0)
            {
                cts.SafeCancelAndDispose();
                cts = new CancellationTokenSource();
                cts.Token.ThrowIfCancellationRequested();

                isDisplaying = true;
                INotification notification = notificationQueue.Dequeue();
                viewInstance.NotificationView.HeaderText.text = notification.GetHeader();
                viewInstance.NotificationView.TitleText.text = notification.GetTitle();
                viewInstance.NotificationView.NotificationType = notification.Type;
                ProcessCustomMetadata(notification);
                if(!string.IsNullOrEmpty(notification.GetThumbnail()))
                    thumbnailImageController.RequestImage(notification.GetThumbnail());

                viewInstance.NotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);

                try
                {
                    viewInstance.NotificationViewCanvasGroup.interactable = true;
                    viewInstance.NotificationViewCanvasGroup.blocksRaycasts = true;
                    await viewInstance.NotificationViewCanvasGroup.DOFade(1, ANIMATION_DURATION).ToUniTask(cancellationToken: cts.Token);
                    await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: cts.Token);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    viewInstance.NotificationViewCanvasGroup.interactable = false;
                    viewInstance.NotificationViewCanvasGroup.blocksRaycasts = false;
                    await viewInstance.NotificationViewCanvasGroup.DOFade(0, ANIMATION_DURATION).ToUniTask();
                }
            }
            isDisplaying = false;
        }

        private void ProcessCustomMetadata(INotification notification)
        {
            switch (notification.Type)
            {
                case NotificationType.REWARD_ASSIGNMENT:
                    viewInstance.NotificationView.NotificationImageBackground.sprite = rarityBackgroundMapping.GetTypeImage(((RewardAssignedNotification)notification).Metadata.Rarity);
                    break;
            }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
