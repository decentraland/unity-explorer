using System;
using Cysharp.Threading.Tasks;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using MVC;

namespace DCL.Backpack.Gifting.Notifications
{
    public class GiftNotificationOpenerController : IDisposable
    {
        private readonly IMVCManager mvcManager;

        public GiftNotificationOpenerController(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;

            // Subscribe to the click event
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(
                NotificationType.GIFT_RECEIVED,
                OnGiftNotificationClicked);
        }

        public void Dispose()
        {
            // Unsubscribe to prevent memory leaks
            // Note: NotificationsBusController usually doesn't have an Unsubscribe method exposed cleanly 
            // for specific types in some versions, check your interface. 
            // If not available, ensure this controller lives as long as the app.
        }

        private void OnGiftNotificationClicked(object[] parameters)
        {
            // Validation
            if (parameters.Length == 0 || parameters[0] is not GiftReceivedNotification notification)
                return;

            // Show the Popup
            mvcManager.ShowAsync(
                GiftReceivedPopupController.IssueCommand(notification.Metadata)
            ).Forget();
        }
    }
}