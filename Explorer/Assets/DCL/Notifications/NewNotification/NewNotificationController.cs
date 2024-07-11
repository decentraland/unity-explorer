using Cysharp.Threading.Tasks;
using DCL.Notification.NotificationsBus;
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
        private readonly Queue<INotification> notificationQueue = new ();
        private bool isDisplaying = false;

        public NewNotificationController(
            ViewFactoryMethod viewFactory,
            INotificationsBusController notificationsBusController
            ) : base(viewFactory)
        {
            this.notificationsBusController = notificationsBusController;
            notificationsBusController.OnNotificationAdded += QueueNewNotification;
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
                viewInstance.NotificationView.TitleText.text = notification.GetTitle();
                viewInstance.NotificationView.TitleText.text = notification.GetHeader();
                await viewInstance.NotificationViewCanvasGroup.DOFade(1, 0.5f).ToUniTask();
                await UniTask.Delay(TimeSpan.FromSeconds(3));
                await viewInstance.NotificationViewCanvasGroup.DOFade(0, 0.5f).ToUniTask();
            }
            isDisplaying = false;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
