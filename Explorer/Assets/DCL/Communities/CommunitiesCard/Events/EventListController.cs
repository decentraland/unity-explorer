using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.CommunicationData.URLHelpers;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Communities.CommunityCreation;
using DCL.Communities.EventInfo;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.Utilities.Extensions;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Types;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetCommunityResponse.CommunityData;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;
using Notifications = DCL.NotificationsBusController.NotificationsBus;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListController : CommunityFetchingControllerBase<PlaceAndEventDTO, EventListView>
    {
        private const int PAGE_SIZE = 20;

        private const string LINK_COPIED_MESSAGE = "Link copied to clipboard!";
        private const string INTERESTED_CHANGED_ERROR_MESSAGE = "There was an error changing your interest on the event. Please try again.";
        private const string FAILED_EVENTS_FETCHING_ERROR_MESSAGE = "There was an error fetching the community's events. Please try again.";
        private const string FAILED_EVENTS_PLACES_FETCHING_ERROR_MESSAGE = "There was an error fetching the community events places. Please try again.";

        private readonly EventListView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IEventsApiService eventsApiService;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly IRealmNavigator realmNavigator;
        private readonly IMVCManager mvcManager;
        private readonly SectionFetchData<PlaceAndEventDTO> eventsFetchData = new (PAGE_SIZE);
        private readonly List<string> eventPlaceIds = new (PAGE_SIZE);
        private readonly Dictionary<string, PlaceInfo> placeInfoCache = new (PAGE_SIZE);
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly string createEventFormat;

        private CommunityData? communityData = null;
        private CancellationTokenSource eventCardOperationsCts = new ();

        protected override SectionFetchData<PlaceAndEventDTO> currentSectionFetchData => eventsFetchData;

        public EventListController(EventListView view,
            IEventsApiService eventsApiService,
            IPlacesAPIService placesAPIService,
            ThumbnailLoader thumbnailLoader,
            IMVCManager mvcManager,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            IRealmNavigator realmNavigator,
            IDecentralandUrlsSource decentralandUrlsSource) : base(view, PAGE_SIZE)
        {
            this.view = view;
            this.eventsApiService = eventsApiService;
            this.placesAPIService = placesAPIService;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            this.realmNavigator = realmNavigator;
            this.mvcManager = mvcManager;
            this.thumbnailLoader = thumbnailLoader;

            createEventFormat = $"{decentralandUrlsSource.Url(DecentralandUrl.EventsWebpage)}/submit?community_id={{0}}";

            view.InitList(thumbnailLoader, cancellationToken);

            view.OpenWizardRequested += OnOpenWizardRequested;
            view.MainButtonClicked += OnMainButtonClicked;
            view.JumpInButtonClicked += OnJumpInButtonClicked;
            view.InterestedButtonClicked += OnInterestedButtonClicked;
            view.EventShareButtonClicked += OnEventShareButtonClicked;
            view.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
            view.CreateEventRequested += OnCreateEventButtonClicked;
        }

        public override void Dispose()
        {
            view.OpenWizardRequested -= OnOpenWizardRequested;
            view.MainButtonClicked -= OnMainButtonClicked;
            view.JumpInButtonClicked -= OnJumpInButtonClicked;
            view.InterestedButtonClicked -= OnInterestedButtonClicked;
            view.EventShareButtonClicked -= OnEventShareButtonClicked;
            view.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
            view.CreateEventRequested -= OnCreateEventButtonClicked;

            eventCardOperationsCts.SafeCancelAndDispose();

            base.Dispose();
        }

        private void OnCreateEventButtonClicked() =>
            webBrowser.OpenUrl(string.Format(createEventFormat, communityData?.id));

        private void OnEventCopyLinkButtonClicked(PlaceAndEventDTO eventData)
        {
            clipboard.Set(EventUtilities.GetEventCopyLink(eventData.Event));

            Notifications.NotificationsBusController.Instance.AddNotification(new DefaultSuccessNotification(LINK_COPIED_MESSAGE));
        }

        private void OnEventShareButtonClicked(PlaceAndEventDTO eventData) =>
            webBrowser.OpenUrl(EventUtilities.GetEventShareLink(eventData.Event));

        private void OnInterestedButtonClicked(PlaceAndEventDTO eventData, EventListItemView eventItemView)
        {
            eventCardOperationsCts = eventCardOperationsCts.SafeRestart();
            UpdateUserInterestedAsync(eventCardOperationsCts.Token).Forget();
            return;

            async UniTaskVoid UpdateUserInterestedAsync(CancellationToken ct)
            {
                var result = eventData.Event.attending
                    ? await eventsApiService.MarkAsNotInterestedAsync(eventData.Event.id, ct)
                                            .SuppressToResultAsync(ReportCategory.COMMUNITIES)
                    : await eventsApiService.MarkAsInterestedAsync(eventData.Event.id, ct)
                                            .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                {
                    eventItemView.UpdateInterestedButtonState();
                    Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(INTERESTED_CHANGED_ERROR_MESSAGE));
                    return;
                }

                eventData.Event.attending = !eventData.Event.attending;
                eventData.Event.total_attendees += eventData.Event.attending ? 1 : -1;

                eventItemView.UpdateInterestedCounter();
                eventItemView.UpdateInterestedButtonState();
            }
        }

        private void OnJumpInButtonClicked(PlaceAndEventDTO eventData)
        {
            eventCardOperationsCts = eventCardOperationsCts.SafeRestart();

            if (!string.IsNullOrWhiteSpace(eventData.Place.world_name))
                realmNavigator.TryChangeRealmAsync(URLDomain.FromString(new ENS(eventData.Place.world_name).ConvertEnsToWorldUrl()), eventCardOperationsCts.Token).Forget();
            else
                realmNavigator.TeleportToParcelAsync(eventData.Place.base_position_processed, eventCardOperationsCts.Token, false).Forget();
        }

        private void OnMainButtonClicked(PlaceAndEventDTO eventData) =>
            mvcManager.ShowAndForget(EventInfoController.IssueCommand(new EventInfoParameter(eventData.Event, eventData.Place)), cancellationToken);

        public override void Reset()
        {
            communityData = null;
            eventsFetchData.Reset();
            view.SetCanModify(false);
            base.Reset();
        }

        private void OnOpenWizardRequested()
        {
            mvcManager.ShowAsync(
                CommunityCreationEditionController.IssueCommand(new CommunityCreationEditionParameter(
                    canCreateCommunities: true,
                    communityId: communityData!.Value.id,
                    thumbnailLoader.Cache)));
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            Result<EventWithPlaceIdDTOListResponse> eventResponse = await eventsApiService.GetCommunityEventsAsync(communityData!.Value.id, eventsFetchData.PageNumber, PAGE_SIZE, ct)
                                                                                                 .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!eventResponse.Success)
            {
                //If the request fails, we restore the previous page number in order to retry the same request next time
                eventsFetchData.PageNumber--;
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(FAILED_EVENTS_FETCHING_ERROR_MESSAGE));
                return eventsFetchData.TotalToFetch;
            }

            if (eventResponse.Value.data.total == 0)
                return 0;

            eventPlaceIds.Clear();

            foreach (var item in eventResponse.Value.data.events)
                eventPlaceIds.Add(item.place_id);

            Result<PlacesData.PlacesAPIResponse> placesResponse = await placesAPIService.GetPlacesByIdsAsync(eventPlaceIds, ct)
                                                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!placesResponse.Success)
            {
                //If the request fails, we restore the previous page number in order to retry the same request next time
                eventsFetchData.PageNumber--;
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(FAILED_EVENTS_PLACES_FETCHING_ERROR_MESSAGE));
                return eventsFetchData.TotalToFetch;
            }

            placeInfoCache.Clear();

            foreach (var place in placesResponse.Value.data)
                placeInfoCache.Add(place.id, place);

            foreach (var item in eventResponse.Value.data.events)
                eventsFetchData.Items.Add(new PlaceAndEventDTO
                {
                    Place = placeInfoCache[item.place_id],
                    Event = item
                });

            return eventResponse.Value.data.total;
        }

        public void ShowEvents(CommunityData community, CancellationToken token)
        {
            cancellationToken = token;
            communityData = community;
            view.SetCanModify(community.role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            FetchNewDataAsync(token).Forget();
        }
    }
}
