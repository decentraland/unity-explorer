using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine.Pool;
using Utility;

namespace DCL.Navmap
{
    public class EventInfoPanelController
    {
        private readonly EventInfoPanelView view;
        private readonly INavmapBus navmapBus;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IEventsApiService eventsApiService;
        private readonly ObjectPool<EventScheduleElementView> scheduleElementPool;
        private readonly IUserCalendar userCalendar;
        private readonly ImageController thumbnailController;
        private readonly MultiStateButtonController interestedButtonController;
        private readonly List<EventScheduleElementView> scheduleElements = new ();
        private PlacesData.PlaceInfo? place;
        private EventDTO? @event;
        private CancellationTokenSource? interestedCancellationToken;
        private CancellationTokenSource? updateLayoutCancellationToken;

        public EventInfoPanelController(EventInfoPanelView view,
            IWebRequestController webRequestController,
            INavmapBus navmapBus,
            IChatMessagesBus chatMessagesBus,
            IEventsApiService eventsApiService,
            ObjectPool<EventScheduleElementView> scheduleElementPool,
            IUserCalendar userCalendar)
        {
            this.view = view;
            this.navmapBus = navmapBus;
            this.chatMessagesBus = chatMessagesBus;
            this.eventsApiService = eventsApiService;
            this.scheduleElementPool = scheduleElementPool;
            this.userCalendar = userCalendar;
            thumbnailController = new ImageController(view.Thumbnail, webRequestController);
            interestedButtonController = new MultiStateButtonController(view.InterestedButton, true);
            interestedButtonController.OnButtonClicked += SetInterested;
            view.ShareButton.onClick.AddListener(Share);
            view.JumpInButton.onClick.AddListener(JumpIn);
        }

        public void Show()
        {
            view.gameObject.SetActive(true);
        }

        public void Hide()
        {
            view.gameObject.SetActive(false);
        }

        public void Set(EventDTO @event, PlacesData.PlaceInfo place)
        {
            this.place = place;
            this.@event = @event;
            view.EventNameLabel.text = @event.name;
            view.LiveContainer.SetActive(@event.live);
            view.InterestedButton.gameObject.SetActive(!@event.live);
            view.JumpInButton.gameObject.SetActive(@event.live);
            view.ScheduleLabel.gameObject.SetActive(!@event.live);
            view.AttendeeContainer.SetActive(!@event.live);

            var schedule = "";

            if (DateTime.TryParse(@event.start_at, null, DateTimeStyles.RoundtripKind, out DateTime startAt))
            {
                schedule = @event.live
                    ? $"Event started {(DateTime.UtcNow - startAt).TotalMinutes} min ago"
                    // TODO: we might need to convert to local, currently R:RFC1123 Fri, 18 Apr 2008 20:30:00 GMT
                    : startAt.ToString("R");
            }

            if (@event.live)
            {
                view.LiveScheduleLabel.text = schedule;
                view.LiveUserCountLabel.text = place.user_count.ToString();
            }
            else
                view.ScheduleLabel.text = schedule;

            view.AttendingUserCountLabel.text = @event.total_attendees.ToString();
            interestedButtonController.SetButtonState(@event.attending);
            view.HostAndPlaceLabel.text = $"hosted by <b>{@event.user_name}</b> - at <b>{place.title} ({@event.x}, {@event.y})</b>";
            view.DescriptionLabel.text = @event.description;
            thumbnailController.RequestImage(@event.image);

            updateLayoutCancellationToken = updateLayoutCancellationToken.SafeRestart();
            view.LayoutRoot.ForceUpdateLayoutAsync(updateLayoutCancellationToken.Token).Forget();

            ClearScheduleElements();

            DateTime.TryParse(@event.next_start_at, null, DateTimeStyles.RoundtripKind, out DateTime nextStartAt);

            foreach (string dateStr in @event.recurrent_dates)
            {
                if (!DateTime.TryParse(dateStr, null, DateTimeStyles.RoundtripKind, out DateTime date)) continue;
                if (date < nextStartAt) continue;

                EventScheduleElementView element = scheduleElementPool.Get();

                // TODO: we might need to convert to local, currently R:RFC1123 Fri, 18 Apr 2008 20:30:00 GMT
                element.DateLabel.text = date.ToString("R");

                element.AddToCalendarButton.onClick.AddListener(() => AddRecurrentEventToCalendar(date));
                scheduleElements.Add(element);
            }
        }

        private void AddRecurrentEventToCalendar(DateTime startAt)
        {
            // Same link as https://decentraland.org/events/event?id=... website
            var description = $"jump in: https://play.decentraland.org/?position={@event?.x},{@event?.y}";

            if (!DateTime.TryParse(@event?.next_start_at, null, DateTimeStyles.RoundtripKind, out DateTime nextStartAt)) return;
            if (!DateTime.TryParse(@event?.next_finish_at, null, DateTimeStyles.RoundtripKind, out DateTime nextFinishAt)) return;

            TimeSpan duration = nextFinishAt - nextStartAt;

            userCalendar.Add(@event?.name, description, startAt, startAt + duration);
        }

        private void ClearScheduleElements()
        {
            foreach (EventScheduleElementView scheduleElement in scheduleElements)
            {
                scheduleElement.AddToCalendarButton.onClick.RemoveAllListeners();
                scheduleElementPool.Release(scheduleElement);
            }

            scheduleElements.Clear();
        }

        private void SetInterested(bool interested)
        {
            interestedCancellationToken = interestedCancellationToken.SafeRestart();
            SetInterestedAsync(interestedCancellationToken.Token).Forget();
            return;

            async UniTaskVoid SetInterestedAsync(CancellationToken ct)
            {
                if (interested)
                    await eventsApiService.MarkAsInterestedAsync(@event?.id, ct);
                else
                    await eventsApiService.MarkAsNotInterestedAsync(@event?.id, ct);

                interestedButtonController.SetButtonState(interested);
            }
        }

        private void Share()
        {

        }

        private void JumpIn()
        {
            navmapBus.JumpIn(place!);
            chatMessagesBus.Send($"/{ChatCommandsUtils.COMMAND_GOTO} {@event?.x},{@event?.y}", "jump in");
        }
    }
}
