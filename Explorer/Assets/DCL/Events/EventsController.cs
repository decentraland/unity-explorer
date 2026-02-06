using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.Friends;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using MVC;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.Events
{
    public class EventsController : ISection, IDisposable
    {
        private const string GET_FRIENDS_ERROR_MESSAGE = "There was an error loading friends. Please try again.";
        private const string GET_MY_COMMUNITIES_ERROR_MESSAGE = "There was an error loading My Communities. Please try again.";

        public event Action<EventsSection, DateTime>? SectionOpen;
        public event Action? EventsClosed;

        public  DateTime CurrentCalendarFromDate { get; set; }

        private readonly EventsView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly CommunitiesDataProvider communitiesDataProvider;

        private bool isSectionActivated;
        private bool isFriendsAndCommunitiesLoaded;
        private readonly EventsCalendarController eventsCalendarController;
        private readonly EventsByDayController eventsByDayController;
        private readonly EventsStateService eventsStateService;

        public EventsController(
            EventsView view,
            ICursor cursor,
            HttpEventsApiService eventsApiService,
            IPlacesAPIService placesAPIService,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            ThumbnailLoader thumbnailLoader,
            EventCardActionsController eventCardActionsController,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ObjectProxy<IFriendsService> friendServiceProxy,
            CommunitiesDataProvider communitiesDataProvider)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;

            eventsStateService = new EventsStateService();
            eventsCalendarController = new EventsCalendarController(view.EventsCalendarView, this, eventsApiService, placesAPIService, eventsStateService, mvcManager, thumbnailLoader, eventCardActionsController, profileRepositoryWrapper);
            eventsByDayController = new EventsByDayController(view.EventsByDayView, this, eventsApiService, placesAPIService, eventsStateService, mvcManager, thumbnailLoader, profileRepositoryWrapper);

            view.CreateButtonClicked += GoToCreateEventPage;
        }

        public void Dispose()
        {
            eventsCalendarController.Dispose();
            eventsByDayController.Dispose();

            view.CreateButtonClicked -= GoToCreateEventPage;
        }

        public void Activate()
        {
            if (isSectionActivated)
                return;

            isSectionActivated = true;
            view.SetViewActive(true);
            cursor.Unlock();
            OpenSection(EventsSection.CALENDAR);
        }

        public void Deactivate()
        {
            isSectionActivated = false;
            view.SetViewActive(false);
            EventsClosed?.Invoke();
            eventsStateService.ClearAllFriends();
            eventsStateService.ClearMyCommunities();
            isFriendsAndCommunitiesLoaded = false;
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void OpenSection(EventsSection section, DateTime? fromDate = null)
        {
            view.OpenSection(section);

            DateTime todayAtTheBeginningOfTheDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 0, 0, 0, DateTimeKind.Local);
            SectionOpen?.Invoke(section, fromDate ?? todayAtTheBeginningOfTheDay);
        }

        public async UniTask RefreshFriendsAndCommunitiesDataAsync(CancellationToken ct)
        {
            if (isFriendsAndCommunitiesLoaded)
                return;

            await GetAllFriendsAsync(ct);
            await GetMyCommunitiesAsync(ct);

            isFriendsAndCommunitiesLoaded = true;
        }

        private async UniTask GetAllFriendsAsync(CancellationToken ct)
        {
            if (!friendServiceProxy.Configured)
                return;

            var result = await friendServiceProxy.StrictObject
                                                 .GetFriendsAsync(0, 1000, ct)
                                                 .SuppressToResultAsync(ReportCategory.EVENTS);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_FRIENDS_ERROR_MESSAGE));
                return;
            }

            eventsStateService.SetAllFriends(result.Value.Friends.ToList());
        }

        private async UniTask GetMyCommunitiesAsync(CancellationToken ct)
        {
            var result = await communitiesDataProvider.GetUserCommunitiesAsync(
                                                           name: string.Empty,
                                                           onlyMemberOf: true,
                                                           pageNumber: 1,
                                                           elementsPerPage: 1000,
                                                           ct: ct,
                                                           includeRequestsReceivedPerCommunity: true)
                                                      .SuppressToResultAsync(ReportCategory.EVENTS);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_MY_COMMUNITIES_ERROR_MESSAGE));
                return;
            }

            eventsStateService.SetMyCommunities(result.Value.data.results.ToList());
        }

        public void GoToCreateEventPage() =>
            webBrowser.OpenUrl($"{decentralandUrlsSource.Url(DecentralandUrl.EventsWebpage)}/submit");
    }
}
