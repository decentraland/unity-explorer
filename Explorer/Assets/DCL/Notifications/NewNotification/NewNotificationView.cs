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

        [field: SerializeField]
        public BadgeNotificationView BadgeNotificationView { get; private set; }

        [field: SerializeField]
        public Animator BadgeNotificationAnimator { get; private set; }

        [field: SerializeField]
        public CanvasGroup BadgeNotificationViewCanvasGroup { get; private set; }

        [field: SerializeField]
        public FriendsNotificationView FriendsNotificationView { get; private set; }

        [field: SerializeField]
        public CanvasGroup FriendsNotificationViewCanvasGroup { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsNotificationView MarketplaceCreditsNotificationView { get; private set; }

        [field: SerializeField]
        public Animator MarketplaceCreditsNotificationAnimator { get; private set; }

        [field: SerializeField]
        public CanvasGroup MarketplaceCreditsNotificationViewCanvasGroup { get; private set; }
    }
}
