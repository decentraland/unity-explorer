using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.CommunicationData.URLHelpers;
using DCL.Communities.EventInfo;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utilities.Extensions;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Events
{
    public class EventCardActionsController
    {
        public event Action<IEventDTO>? EventSetAsInterested;
        public event Action<IEventDTO>? AddEventToCalendarClicked;
        public event Action<IEventDTO>? JumpedInEventPlace;
        public event Action<IEventDTO>? EventShared;
        public event Action<IEventDTO>? EventLinkCopied;

        private const string INTERESTED_CHANGED_ERROR_MESSAGE = "There was an error changing your interest on the event. Please try again.";
        private const string LINK_COPIED_MESSAGE = "Link copied to clipboard!";

        private readonly HttpEventsApiService eventsApiService;
        private readonly IWebBrowser webBrowser;
        private readonly IRealmNavigator realmNavigator;
        private readonly ISystemClipboard clipboard;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public EventCardActionsController(
            HttpEventsApiService eventsApiService,
            IWebBrowser webBrowser,
            IRealmNavigator realmNavigator,
            ISystemClipboard clipboard,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.eventsApiService = eventsApiService;
            this.webBrowser = webBrowser;
            this.realmNavigator = realmNavigator;
            this.clipboard = clipboard;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTaskVoid SetEventAsInterestedAsync(IEventDTO eventData, EventCardView? eventCardView, EventDetailPanelView? eventDetailPanelView, CancellationToken ct)
        {
            var result = eventData.Attending
                ? await eventsApiService.MarkAsNotInterestedAsync(eventData.Id, ct)
                                        .SuppressToResultAsync(ReportCategory.EVENTS)
                : await eventsApiService.MarkAsInterestedAsync(eventData.Id, ct)
                                        .SuppressToResultAsync(ReportCategory.EVENTS);

            if (!result.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(INTERESTED_CHANGED_ERROR_MESSAGE));
                return;
            }

            eventData.Attending = !eventData.Attending;
            eventData.Total_attendees += eventData.Attending ? 1 : -1;

            eventCardView?.UpdateInterestedButtonState(eventData.Attending);
            eventCardView?.UpdateVisuals();
            eventDetailPanelView?.UpdateInterestedButtonState(eventData.Attending);

            EventSetAsInterested?.Invoke(eventData);
        }

        public void AddEventToCalendar(IEventDTO eventData)
        {
            webBrowser.OpenUrl($"{EventUtilities.GetEventAddToCalendarLink(eventData)}&utm_source=explorer&utm_campaign=discover");
            AddEventToCalendarClicked?.Invoke(eventData);
        }

        public void AddEventToCalendar(IEventDTO eventData, DateTime utcStart)
        {
            webBrowser.OpenUrl($"{EventUtilities.GetEventAddToCalendarLink(eventData, utcStart)}&utm_source=explorer&utm_campaign=discover");
            AddEventToCalendarClicked?.Invoke(eventData);
        }

        public void JumpInEvent(IEventDTO eventData, CancellationToken ct)
        {
            if (eventData.World)
                realmNavigator.TryChangeRealmAsync(URLDomain.FromString(new ENS(eventData.Server).ConvertEnsToWorldUrl(decentralandUrlsSource.Url(DecentralandUrl.WorldContentServer))), ct).Forget();
            else
                realmNavigator.TeleportToParcelAsync(new Vector2Int(eventData.X, eventData.Y), ct, false).Forget();

            JumpedInEventPlace?.Invoke(eventData);
        }

        public void ShareEvent(IEventDTO eventData)
        {
            webBrowser.OpenUrl($"{EventUtilities.GetEventShareLink(eventData)}&utm_source=explorer&utm_campaign=discover");
            EventShared?.Invoke(eventData);
        }

        public void CopyEventLink(IEventDTO eventData)
        {
            clipboard.Set(EventUtilities.GetEventCopyLink(eventData));
            NotificationsBusController.Instance.AddNotification(new DefaultSuccessNotification(LINK_COPIED_MESSAGE));
            EventLinkCopied?.Invoke(eventData);
        }
    }
}
