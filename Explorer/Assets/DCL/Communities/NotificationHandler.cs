using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Communities.CommunitiesCard;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using System.Web;
using UnityEngine;
using Utility;

namespace DCL.Communities
{
    public class NotificationHandler : IDisposable
    {
        private const string EVENT_CREATED_REALM_KEY = "realm";
        private const string EVENT_CREATED_POSITION_KEY = "position";

        private readonly IMVCManager mvcManager;
        private readonly IRealmNavigator realmNavigator;

        private CancellationTokenSource eventCreatedCts = new ();
        private CancellationTokenSource eventStartsCts = new ();

        public NotificationHandler(INotificationsBusController notificationsBusController,
            IMVCManager mvcManager,
            IRealmNavigator realmNavigator)
        {
            this.mvcManager = mvcManager;
            this.realmNavigator = realmNavigator;

            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.EVENT_CREATED, EventCreatedClicked);
            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.EVENTS_STARTED, EventStartSoonClicked);
        }

        public void Dispose()
        {
            eventCreatedCts.SafeCancelAndDispose();
            eventStartsCts.SafeCancelAndDispose();
        }

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

        private void EventCreatedClicked(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not CommunityEventCreatedNotification)
                return;

            eventCreatedCts = eventCreatedCts.SafeRestart();

            CommunityEventCreatedNotification notification = (CommunityEventCreatedNotification)parameters[0];
            mvcManager.ShowAndForget(CommunityCardController.IssueCommand(new CommunityCardParameter(notification.Metadata.CommunityId)));
        }
    }
}
