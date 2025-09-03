using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Communities
{
    public class NotificationHandler : IDisposable
    {
        private readonly IRealmNavigator realmNavigator;

        private CancellationTokenSource eventStartsCts = new ();

        public NotificationHandler(NotificationsBusController.NotificationsBus.NotificationsBusController notificationsBusController,
            IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;

            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.COMMUNITY_EVENT_ABOUT_TO_START, EventStartSoonClicked);
        }

        public void Dispose() =>
            eventStartsCts.SafeCancelAndDispose();

        private void EventStartSoonClicked(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not CommunityEventSoonNotification)
                return;

            eventStartsCts = eventStartsCts.SafeRestart();

            CommunityEventSoonNotification notification = (CommunityEventSoonNotification)parameters[0];

            if (notification.Metadata.World)
                realmNavigator.TryChangeRealmAsync(URLDomain.FromString(new ENS(notification.Metadata.WorldName).ConvertEnsToWorldUrl()), eventStartsCts.Token).Forget();
            else
                realmNavigator.TeleportToParcelAsync(new Vector2Int(notification.Metadata.X, notification.Metadata.Y), eventStartsCts.Token, false).Forget();
        }
    }
}
