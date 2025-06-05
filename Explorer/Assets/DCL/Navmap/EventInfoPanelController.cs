using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Utilities;
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
        private readonly SharePlacesAndEventsContextMenuController shareContextMenu;
        private readonly IWebBrowser webBrowser;
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
            IUserCalendar userCalendar,
            SharePlacesAndEventsContextMenuController shareContextMenu,
            IWebBrowser webBrowser)
        {
            this.view = view;
            this.navmapBus = navmapBus;
            this.chatMessagesBus = chatMessagesBus;
            this.eventsApiService = eventsApiService;
            this.scheduleElementPool = scheduleElementPool;
            this.userCalendar = userCalendar;
            this.shareContextMenu = shareContextMenu;
            this.webBrowser = webBrowser;
            thumbnailController = new ImageController(view.Thumbnail, webRequestController);
            interestedButtonController = new MultiStateButtonController(view.InterestedButton, true);
            interestedButtonController.OnButtonClicked += SetInterested;
            view.ShareButton.onClick.AddListener(Share);
            view.JumpInButton.onClick.AddListener(JumpIn);
            view.EventsScrollRect.SetScrollSensitivityBasedOnPlatform();
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
                if (@event.live)
                {
                    TimeSpan elapsed = DateTime.UtcNow - startAt;

                    if (elapsed.TotalDays >= 1)
                        schedule = $"Event started {(int)elapsed.TotalDays} day ago";
                    else if (elapsed.TotalHours >= 1)
                        schedule = $"Event started {(int)elapsed.TotalHours} hour ago";
                    else
                        schedule = $"Event started {(int)elapsed.TotalMinutes} min ago";
                }
                else
                    // TODO: we might need to convert to local, currently R:RFC1123 Fri, 18 Apr 2008 20:30:00 GMT
                    schedule = startAt.ToString("R");
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
            view.DescriptionLabel.ConvertUrlsToClickeableLinks(OpenUrl);
            thumbnailController.RequestImage(@event.image);

            updateLayoutCancellationToken = updateLayoutCancellationToken.SafeRestart();
            view.LayoutRoot.ForceUpdateLayoutAsync(updateLayoutCancellationToken.Token).Forget();

            ClearScheduleElements();
            AddRecurrentEvents(@event);
        }

        private void OpenUrl(string url) =>
            webBrowser.OpenUrl(new Uri(url));

        private void AddRecurrentEvents(EventDTO @event)
        {
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
            var description = $"jump in: https://decentraland.org/jump/?position={@event?.x},{@event?.y}";

            DateTime nextStartAt = DateTime.Parse(@event?.next_start_at, null, DateTimeStyles.RoundtripKind);
            DateTime nextFinishAt = DateTime.Parse(@event?.next_finish_at, null, DateTimeStyles.RoundtripKind);
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
            shareContextMenu.Set(@event!.Value);
            shareContextMenu.Show(view.SharePivot);
        }

        private void JumpIn()
        {
            navmapBus.JumpIn(place!);
            chatMessagesBus.Send(ChatChannel.NEARBY_CHANNEL, $"/{ChatCommandsUtils.COMMAND_GOTO} {@event?.x},{@event?.y}", "jump in");
        }
    }
}
