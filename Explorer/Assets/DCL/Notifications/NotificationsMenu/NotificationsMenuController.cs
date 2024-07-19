using Cysharp.Threading.Tasks;
using DCL.Notification.NotificationEntry;
using DCL.Notification.NotificationsBus;
using SuperScrollView;
using System.Collections.Generic;

namespace DCL.Notification.NotificationsMenu
{
    public class NotificationsMenuController
    {
        private readonly NotificationsMenuView view;
        private readonly NotificationsRequestController notificationsRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private List<INotification> notifications = new List<INotification>();

        public NotificationsMenuController(
            NotificationsMenuView view,
            NotificationsRequestController notificationsRequestController,
            INotificationsBusController notificationsBusController)
        {
            this.view = view;
            this.notificationsRequestController = notificationsRequestController;
            this.notificationsBusController = notificationsBusController;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            InitialNotificationRequest().Forget();
        }

        public void ToggleNotificationsPanel()
        {
            view.gameObject.SetActive(!view.gameObject.activeSelf);
        }

        private async UniTaskVoid InitialNotificationRequest()
        {
            List<INotification> RequestNotifications = await notificationsRequestController.RequestNotifications();

            foreach (INotification requestNotification in RequestNotifications)
            {
                notifications.Add(requestNotification);
            }
            view.LoopList.SetListItemCount(notifications.Count, false);
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int arg2)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            NotificationView notificationView = listItem!.GetComponent<NotificationView>();
            INotification notificationData = notifications[arg2];

            notificationView.HeaderText.text = notificationData.GetHeader();
            notificationView.TitleText.text = notificationData.GetTitle();
            notificationView.NotificationType = notificationData.Type;
            return listItem;
        }
    }
}
