using DCL.Notification.NotificationEntry;
using MVC;
using UnityEngine;

namespace DCL.Notification.NewNotification
{
    public class NewNotificationView : ViewBase, IView
    {
        [field: SerializeField]
        public NotificationView NotificationView { get; private set; }

        [field: SerializeField]
        public CanvasGroup NotificationViewCanvasGroup { get; private set; }
    }
}
