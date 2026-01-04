using Cysharp.Threading.Tasks;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using MVC;

namespace DCL.Backpack.Gifting.Notifications
{
    public class GiftNotificationOpenerController
    {
        private readonly IMVCManager mvcManager;

        public GiftNotificationOpenerController(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
            
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(
                NotificationType.TRANSFER_RECEIVED,
                OnGiftNotificationClicked);
        }

        private void OnGiftNotificationClicked(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not GiftReceivedNotification notification)
                return;

            mvcManager
                .ShowAsync(GiftReceivedPopupController.IssueCommand(notification))
                .Forget();
        }
    }
}