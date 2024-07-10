using Cysharp.Threading.Tasks;
using DCL.Notification.NotificationsBus;
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
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
