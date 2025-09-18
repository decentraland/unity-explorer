using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.NotificationsBusController.NotificationTypes;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using System.Web;
using Notifications = DCL.NotificationsBusController.NotificationsBus;

namespace DCL.Communities
{
    public class NotificationHandler : IDisposable
    {
        private const string EVENT_CREATED_REALM_KEY = "realm";
        private const string EVENT_CREATED_POSITION_KEY = "position";

        private readonly IRealmNavigator realmNavigator;

        private CancellationTokenSource eventStartsCts = new ();

        public NotificationHandler(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;

            Notifications.NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.EVENTS_STARTED, EventStartSoonClicked);
        }

        public void Dispose() =>
            eventStartsCts.SafeCancelAndDispose();

        private void EventStartSoonClicked(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not EventStartedNotification)
                return;

            eventStartsCts = eventStartsCts.SafeRestart();

            EventStartedNotification notification = (EventStartedNotification)parameters[0];

            Uri uri = new Uri(notification.Metadata.Link);
            var queryParams = HttpUtility.ParseQueryString(uri.Query);

            string? worldName = queryParams[EVENT_CREATED_REALM_KEY];
            string? positionString = queryParams[EVENT_CREATED_POSITION_KEY];

            Vector2Int position = default;

            if (positionString != null)
            {
                string[] split = positionString.Split(',');
                position = new Vector2Int(int.Parse(split[0]), int.Parse(split[1]));
            }

            if (worldName != null)
                realmNavigator.TryChangeRealmAsync(URLDomain.FromString(new ENS(worldName).ConvertEnsToWorldUrl()), eventStartsCts.Token, position).Forget();
            else
                realmNavigator.TeleportToParcelAsync(position, eventStartsCts.Token, false).Forget();
        }
    }
}
