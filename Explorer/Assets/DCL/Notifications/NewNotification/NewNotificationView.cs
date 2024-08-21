using DCL.Notifications.NotificationEntry;
using MVC;
using UnityEngine;

namespace DCL.Notifications.NewNotification
{
    public class NewNotificationView : ViewBase, IView
    {
        [field: SerializeField]
        public NotificationView NotificationView { get; private set; }

        [field: SerializeField]
        public SystemNotificationView SystemNotificationView { get; private set; }

        [field: SerializeField]
        public CanvasGroup NotificationViewCanvasGroup { get; private set; }

        [field: SerializeField]
        public CanvasGroup SystemNotificationViewCanvasGroup { get; private set; }
    }
}
