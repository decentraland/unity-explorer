using DCL.EventsApi;
using DCL.PerformanceAndDiagnostics.Analytics;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.Events
{
    public class EventsAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly EventsController eventsController;
        private readonly EventsCalendarController eventsCalendarController;
        private readonly EventCardActionsController eventCardActionsController;

        public EventsAnalytics(
            IAnalyticsController analytics,
            EventsController eventsController,
            EventsCalendarController eventsCalendarController,
            EventCardActionsController eventCardActionsController)
        {
            this.analytics = analytics;
            this.eventsController = eventsController;
            this.eventsCalendarController = eventsCalendarController;
            this.eventCardActionsController = eventCardActionsController;

            eventsController.Activated += OnSectionActivated;
            eventsController.SectionOpen += OnSectionOpen;
            eventsController.CreateEventButtonClicked += OnCreateEventButtonClicked;
            eventsCalendarController.EventCardClicked += OnEventCardClicked;
            eventCardActionsController.EventSetAsInterested += OnEventSetAsInterested;
            eventCardActionsController.AddEventToCalendarClicked += OnAddEventToCalendarClicked;
            eventCardActionsController.JumpedInEventPlace += OnJumpedInEventPlace;
            eventCardActionsController.EventShared += OnEventShared;
            eventCardActionsController.EventLinkCopied += OnEventLinkCopied;
        }

        public void Dispose()
        {
            eventsController.Activated -= OnSectionActivated;
            eventsController.SectionOpen -= OnSectionOpen;
            eventsController.CreateEventButtonClicked -= OnCreateEventButtonClicked;
            eventsCalendarController.EventCardClicked -= OnEventCardClicked;
            eventCardActionsController.EventSetAsInterested -= OnEventSetAsInterested;
            eventCardActionsController.AddEventToCalendarClicked -= OnAddEventToCalendarClicked;
            eventCardActionsController.JumpedInEventPlace -= OnJumpedInEventPlace;
            eventCardActionsController.EventShared -= OnEventShared;
            eventCardActionsController.EventLinkCopied -= OnEventLinkCopied;
        }

        private void OnSectionActivated() =>
            analytics.Track(AnalyticsEvents.Events.EVENTS_SECTION_OPENED, new JObject());

        private void OnSectionOpen(EventsSection section, DateTime fromDate)
        {
            if (section != EventsSection.EVENTS_BY_DAY)
                return;

            analytics.Track(AnalyticsEvents.Events.EVENTS_BY_DAY_OPENED, new JObject
            {
                { "date", fromDate.ToString("yyyy-MM-dd") },
            });
        }

        private void OnCreateEventButtonClicked(bool fromHeaderButton)
        {
            analytics.Track(AnalyticsEvents.Events.EVENT_CREATION_OPENED, new JObject
            {
                { "from_header_button", fromHeaderButton },
            });
        }

        private void OnEventCardClicked(EventDTO eventInfo) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_CARD_CLICKED, GetEventJObject(eventInfo));

        private void OnEventSetAsInterested(IEventDTO eventInfo) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_SET_AS_INTERESTED, GetEventJObject(eventInfo));

        private void OnAddEventToCalendarClicked(IEventDTO eventInfo) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_ADDED_TO_CALENDAR, GetEventJObject(eventInfo));

        private void OnJumpedInEventPlace(IEventDTO eventInfo) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_JUMPED_IN, GetEventJObject(eventInfo));

        private void OnEventShared(IEventDTO eventInfo) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_SHARED, GetEventJObject(eventInfo));

        private void OnEventLinkCopied(IEventDTO eventInfo) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_LINK_COPIED, GetEventJObject(eventInfo));

        private static JObject GetEventJObject(IEventDTO eventInfo) =>
            new()
            {
                { "event_id", eventInfo.Id },
                { "event_name", eventInfo.Name },
                { "event_coords", $"({eventInfo.X}, {eventInfo.Y})" },
                { "highlighted", eventInfo.Highlighted },
            };
    }
}
