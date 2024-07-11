using Cysharp.Threading.Tasks;
using DCL.Notification.NotificationsBus;
using DCL.UI;
using DCL.WebRequests;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Notification.NewNotification
{
    public class NewNotificationController : ControllerBase<NewNotificationView>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationIconTypes notificationIconTypes;
        //private readonly NftTypeIconSO nftTypeIconSo;
        private readonly IWebRequestController webRequestController;
        private readonly Queue<INotification> notificationQueue = new ();
        private bool isDisplaying = false;
        private ImageController thumbnailImageController;

        public NewNotificationController(
            ViewFactoryMethod viewFactory,
            INotificationsBusController notificationsBusController,
            NotificationIconTypes notificationIconTypes,
            //NftTypeIconSO nftTypeIconSO,
            IWebRequestController webRequestController
            ) : base(viewFactory)
        {
            this.notificationsBusController = notificationsBusController;
            this.notificationIconTypes = notificationIconTypes;
            //nftTypeIconSo = nftTypeIconSO;
            this.webRequestController = webRequestController;
            notificationsBusController.OnNotificationAdded += QueueNewNotification;
        }

        protected override void OnViewInstantiated()
        {
            thumbnailImageController = new ImageController(viewInstance.NotificationView.NotificationImage, webRequestController);
        }

        private void QueueNewNotification(INotification newNotification)
        {
            notificationQueue.Enqueue(newNotification);
            if (!isDisplaying)
            {
                DisplayNewNotification().Forget();
            }
        }

        private async UniTaskVoid DisplayNewNotification()
        {
            while (notificationQueue.Count > 0)
            {
                isDisplaying = true;
                INotification notification = notificationQueue.Dequeue();
                viewInstance.NotificationView.HeaderText.text = notification.GetHeader();
                viewInstance.NotificationView.TitleText.text = notification.GetTitle();
                if(!string.IsNullOrEmpty(notification.GetThumbnail()))
                    thumbnailImageController.RequestImage(notification.GetThumbnail());

                viewInstance.NotificationView.NotificationTypeImage.sprite = notificationIconTypes.GetNotificationIcon(notification.Type);
                await viewInstance.NotificationViewCanvasGroup.DOFade(1, 0.5f).ToUniTask();
                await UniTask.Delay(TimeSpan.FromSeconds(3));
                await viewInstance.NotificationViewCanvasGroup.DOFade(0, 0.5f).ToUniTask();
            }
            isDisplaying = false;
        }

        private void ProcessCustomMetadata(INotification notification)
        {
            switch (notification.Type)
            {
                case NotificationType.REWARD_ASSIGNMENT:
                    //viewInstance.NotificationView.NotificationImageBackground.sprite = nftTypeIconSo.GetTypeImage(((RewardAssignedNotification)notification).Metadata.Rarity);
                    break;
            }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
