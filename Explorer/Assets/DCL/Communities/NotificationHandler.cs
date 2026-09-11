using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.TeleportPrompt;
using MVC;
using System;
using System.Collections.Specialized;
using System.Globalization;
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

        private CancellationTokenSource eventStartsCts = new ();

        public NotificationHandler(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;

            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.EVENTS_STARTED, EventStartSoonClicked);
        }

        public void Dispose() =>
            eventStartsCts.SafeCancelAndDispose();

        private void EventStartSoonClicked(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not EventStartedNotification notification)
                return;

            // Parsing must be total: NotificationsBusController.ClickNotification invokes its subscribers as a
            // multicast delegate with no try/catch, so a throw here escapes into the click dispatch and also drops
            // the subscribers registered after this one for the same notification type.
            if (!TryParseDestination(notification.Metadata.Link, out string? worldName, out Vector2Int? parcel))
                return;

            eventStartsCts = eventStartsCts.SafeRestart();

            ConfirmDestinationAsync(worldName, parcel, eventStartsCts.Token).Forget();
        }

        /// <summary>
        /// Reads the destination out of the event link. Every part of that link is authored by whoever created the
        /// event through the open Events API, so nothing is assumed about its shape and a rejected link yields
        /// <c>false</c> instead of an exception.
        /// </summary>
        private static bool TryParseDestination(string? link, out string? worldName, out Vector2Int? parcel)
        {
            worldName = null;
            parcel = null;

            if (!Uri.TryCreate(link, UriKind.Absolute, out Uri? uri))
            {
                ReportHub.LogWarning(ReportCategory.EVENTS, $"Event notification click ignored: link is not an absolute URI ('{link}').");
                return false;
            }

            NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);

            string? realmParam = queryParams[EVENT_CREATED_REALM_KEY];
            string? positionParam = queryParams[EVENT_CREATED_POSITION_KEY];

            if (positionParam != null)
            {
                if (!TryParseParcel(positionParam, out Vector2Int parsedParcel))
                {
                    ReportHub.LogWarning(ReportCategory.EVENTS, $"Event notification click ignored: '{EVENT_CREATED_POSITION_KEY}' is not an 'x,y' parcel ('{positionParam}').");
                    return false;
                }

                parcel = parsedParcel;
            }

            if (realmParam != null)
            {
                // IsEns is case sensitive on the ".eth" suffix, so normalize before validating: the lower-cased name
                // is also what gets resolved downstream, and what the world content server expects.
                string normalizedRealm = realmParam.ToLowerInvariant();

                // The realm has to name an ENS world, which keeps the destination on the official world server that
                // ChatTeleporter resolves it against. Rejecting anything else (a bare URL, a realm alias) stops the
                // link from pointing the client at an arbitrary catalyst.
                if (!normalizedRealm.IsEns())
                {
                    ReportHub.LogWarning(ReportCategory.EVENTS, $"Event notification click ignored: '{EVENT_CREATED_REALM_KEY}' is not an ENS world name ('{realmParam}').");
                    return false;
                }

                worldName = normalizedRealm;
            }

            if (worldName == null && parcel == null)
            {
                ReportHub.LogWarning(ReportCategory.EVENTS, $"Event notification click ignored: link carries no '{EVENT_CREATED_REALM_KEY}' or '{EVENT_CREATED_POSITION_KEY}' ('{link}').");
                return false;
            }

            return true;
        }

        private static bool TryParseParcel(string value, out Vector2Int parcel)
        {
            parcel = default(Vector2Int);

            string[] split = value.Split(',');

            if (split.Length != 2
                || !int.TryParse(split[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(split[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y))
                return false;

            parcel = new Vector2Int(x, y);
            return true;
        }

        /// <summary>
        /// Asks for consent before the client moves anywhere. Approving the prompt is what performs the navigation,
        /// which is the same arrangement the scene <c>changeRealm()</c>, deep link and chat world link paths use.
        /// </summary>
        private async UniTaskVoid ConfirmDestinationAsync(string? worldName, Vector2Int? parcel, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return;

                if (worldName != null)
                    // An empty message leaves the prompt showing its own default confirmation text.
                    await mvcManager.ShowAsync(ChangeRealmPromptController.IssueCommand(new ChangeRealmPromptController.Params(string.Empty, worldName, parcel)), ct);
                else if (parcel.HasValue)
                    await mvcManager.ShowAsync(TeleportPromptController.IssueCommand(new TeleportPromptController.Params(parcel.Value)), ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportHub.LogException(exception, ReportCategory.EVENTS); }
        }
    }
}
