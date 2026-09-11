using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.EventsApi;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Utilities;
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
        private readonly HttpEventsApiService eventsApiService;
        private readonly ObjectPool<EventScheduleElementView> scheduleElementPool;
        private readonly GoogleUserCalendar userCalendar;
        private readonly SharePlacesAndEventsContextMenuController shareContextMenu;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly ImageController thumbnailController;
        private readonly MultiStateButtonController interestedButtonController;
        private readonly List<EventScheduleElementView> scheduleElements = new ();
        private PlacesData.PlaceInfo? place;
        private EventDTO? @event;
        private CancellationTokenSource? interestedCancellationToken;
        private CancellationTokenSource? updateLayoutCancellationToken;

        public EventInfoPanelController(EventInfoPanelView view,
            INavmapBus navmapBus,
            IChatMessagesBus chatMessagesBus,
            HttpEventsApiService eventsApiService,
            ObjectPool<EventScheduleElementView> scheduleElementPool,
            GoogleUserCalendar userCalendar,
            SharePlacesAndEventsContextMenuController shareContextMenu,
            UnityAppWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            ImageControllerProvider imageControllerProvider)
        {
            this.view = view;
            this.navmapBus = navmapBus;
            this.chatMessagesBus = chatMessagesBus;
            this.eventsApiService = eventsApiService;
            this.scheduleElementPool = scheduleElementPool;
            this.userCalendar = userCalendar;
            this.shareContextMenu = shareContextMenu;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            thumbnailController = imageControllerProvider.Create(view.Thumbnail);
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

        public void Set(EventDTO newEvent, PlacesData.PlaceInfo newPlace)
        {
            this.place = newPlace;
            this.@event = newEvent;
            view.EventNameLabel.text = newEvent.name;
            view.LiveContainer.SetActive(newEvent.live);
            view.InterestedButton.gameObject.SetActive(!newEvent.live);
            view.JumpInButton.gameObject.SetActive(newEvent.live);
            view.ScheduleLabel.gameObject.SetActive(!newEvent.live);
            view.AttendeeContainer.SetActive(!newEvent.live);

            var schedule = "";

            if (DateTime.TryParse(newEvent.start_at, null, DateTimeStyles.RoundtripKind, out DateTime startAt))
            {
                if (newEvent.live)
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

            if (newEvent.live)
            {
                view.LiveScheduleLabel.text = schedule;
                view.LiveUserCountLabel.text = newPlace.user_count.ToString();
            }
            else
                view.ScheduleLabel.text = schedule;

            view.AttendingUserCountLabel.text = newEvent.total_attendees.ToString();
            interestedButtonController.SetButtonState(newEvent.attending);
            view.HostAndPlaceLabel.text = $"hosted by <b>{newEvent.user_name}</b> - at <b>{newPlace.title} ({newEvent.x}, {newEvent.y})</b>";
            view.DescriptionLabel.text = newEvent.description;
            view.DescriptionLabel.ConvertUrlsToClickeableLinks(OpenUrl);
            thumbnailController.RequestImage(newEvent.image);

            updateLayoutCancellationToken = updateLayoutCancellationToken.SafeRestart();
            view.LayoutRoot.ForceUpdateLayoutAsync(updateLayoutCancellationToken.Token).Forget();

            ClearScheduleElements();
            AddRecurrentEvents(newEvent);
        }

        private void OpenUrl(string url) =>
            webBrowser.OpenUrlMainThreadOnly(url);

        private void AddRecurrentEvents(EventDTO recurrentEvent)
        {
            DateTime.TryParse(recurrentEvent.next_start_at, null, DateTimeStyles.RoundtripKind, out DateTime nextStartAt);

            foreach (string dateStr in recurrentEvent.recurrent_dates)
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
            string jumpInLink = ShareLinkUtilities.WithReferrer(string.Format(decentralandUrlsSource.Url(DecentralandUrl.JumpInGenesisCityLink), @event?.x, @event?.y));
            var description = $"jump in: {ShareLinkUtilities.AsQueryParameterValue(jumpInLink)}";

            DateTime nextStartAt = DateTime.Parse(@event?.next_start_at, null, DateTimeStyles.RoundtripKind);
            DateTime nextFinishAt = DateTime.Parse(@event?.next_finish_at, null, DateTimeStyles.RoundtripKind);
            TimeSpan duration = nextFinishAt - nextStartAt;

            userCalendar.Add(@event?.name ?? string.Empty, description, startAt, startAt + duration);
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
                if (!@event.HasValue) return;

                if (interested)
                    await eventsApiService.MarkAsInterestedAsync(@event.Value.id, ct);
                else
                    await eventsApiService.MarkAsNotInterestedAsync(@event.Value.id, ct);

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
            chatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, $"/{ChatCommandsUtils.COMMAND_GOTO} {@event?.x},{@event?.y}", ChatMessageOrigin.JumpIn);
        }
    }
}
