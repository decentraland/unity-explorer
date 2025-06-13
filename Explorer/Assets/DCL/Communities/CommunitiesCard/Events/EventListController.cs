using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Types;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListController : CommunityFetchingControllerBase<PlaceAndEventDTO, EventListView>
    {
        private const int PAGE_SIZE = 20;
        private const int WARNING_NOTIFICATION_DURATION_MS = 3000;

        private const string JUMP_IN_GC_LINK = " https://decentraland.org/jump/?position={0},{1}";
        private const string JUMP_IN_WORLD_LINK = " https://decentraland.org/jump/?realm={0}";
        private const string EVENT_WEBSITE_LINK = "https://decentraland.org/events/event/?id={0}";
        private const string TWITTER_NEW_POST_LINK = "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}";
        private const string TWITTER_HASHTAG = "DCLPlace";

        private const string LINK_COPIED_MESSAGE = "Link copied to clipboard!";
        private const string INTERESTED_CHANGED_ERROR_MESSAGE = "There was an error changing your interest on the event. Please try again.";
        private const string FAILED_EVENTS_FETCHING_ERROR_MESSAGE = "There was an error fetching the community's events. Please try again.";
        private const string FAILED_EVENTS_PLACES_FETCHING_ERROR_MESSAGE = "There was an error fetching the community events places. Please try again.";

        private readonly EventListView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IEventsApiService eventsApiService;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly WarningNotificationView inWorldSuccessNotificationView;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly IRealmNavigator realmNavigator;
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private readonly SectionFetchData<PlaceAndEventDTO> eventsFetchData = new (PAGE_SIZE);
        private readonly List<string> eventPlaceIds = new (PAGE_SIZE);
        private readonly Dictionary<string, PlaceInfo> placeInfoCache = new (PAGE_SIZE);

        private CommunityData? communityData = null;
        private CancellationTokenSource eventCardOperationsCts = new ();

        protected override SectionFetchData<PlaceAndEventDTO> currentSectionFetchData => eventsFetchData;

        public EventListController(EventListView view,
            IEventsApiService eventsApiService,
            IPlacesAPIService placesAPIService,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            WarningNotificationView inWorldWarningNotificationView,
            WarningNotificationView inWorldSuccessNotificationView,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            IRealmNavigator realmNavigator,
            ICommunitiesDataProvider communitiesDataProvider) : base(view, PAGE_SIZE)
        {
            this.view = view;
            this.eventsApiService = eventsApiService;
            this.placesAPIService = placesAPIService;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.inWorldSuccessNotificationView = inWorldSuccessNotificationView;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            this.realmNavigator = realmNavigator;
            this.communitiesDataProvider = communitiesDataProvider;

            view.InitList(() => currentSectionFetchData, webRequestController, mvcManager, cancellationToken);

            view.OpenWizardRequested += OnOpenWizardRequested;
            view.MainButtonClicked += OnMainButtonClicked;
            view.JumpInButtonClicked += OnJumpInButtonClicked;
            view.InterestedButtonClicked += OnInterestedButtonClicked;
            view.EventShareButtonClicked += OnEventShareButtonClicked;
            view.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
        }

        public override void Dispose()
        {
            view.OpenWizardRequested -= OnOpenWizardRequested;
            view.MainButtonClicked -= OnMainButtonClicked;
            view.JumpInButtonClicked -= OnJumpInButtonClicked;
            view.InterestedButtonClicked -= OnInterestedButtonClicked;
            view.EventShareButtonClicked -= OnEventShareButtonClicked;
            view.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;

            eventCardOperationsCts.SafeCancelAndDispose();

            base.Dispose();
        }

        private void OnEventCopyLinkButtonClicked(PlaceAndEventDTO eventData)
        {
            clipboard.Set(GetEventCopyLink(eventData));

            inWorldSuccessNotificationView.AnimatedShowAsync(LINK_COPIED_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, cancellationToken).Forget();
        }

        private static string GetEventCopyLink(PlaceAndEventDTO eventData) =>
            eventData.Event.live
                ? GetPlaceJumpInLink(eventData)
                : GetEventWebsiteLink(eventData);

        private static string GetPlaceJumpInLink(PlaceAndEventDTO eventData)
        {
            if (!string.IsNullOrEmpty(eventData.Place.world_name))
                return string.Format(JUMP_IN_WORLD_LINK, eventData.Place.world_name);

            VectorUtilities.TryParseVector2Int(eventData.Place.base_position, out var coordinates);
            return string.Format(JUMP_IN_GC_LINK, coordinates.x, coordinates.y);
        }

        private static string GetEventWebsiteLink(PlaceAndEventDTO eventData) =>
            string.Format(EVENT_WEBSITE_LINK, eventData.Event.id);

        private void OnEventShareButtonClicked(PlaceAndEventDTO eventData) =>
            webBrowser.OpenUrl(string.Format(TWITTER_NEW_POST_LINK, eventData.Event.name, TWITTER_HASHTAG, GetEventCopyLink(eventData)));

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

                if (!result.Success)
                {
                    eventItemView.UpdateInterestedButtonState();
                    await inWorldWarningNotificationView.AnimatedShowAsync(INTERESTED_CHANGED_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct);
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
            webBrowser.OpenUrl(GetEventWebsiteLink(eventData));

        public override void Reset()
        {
            communityData = null;
            eventsFetchData.Reset();
            view.SetCanModify(false);
            base.Reset();
        }

        private void OnOpenWizardRequested()
        {
            //TODO: open community wizard
            throw new NotImplementedException();
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            Result<CommunityEventsResponse> eventResponse = await communitiesDataProvider.GetCommunityEventsAsync(communityData!.Value.id, eventsFetchData.pageNumber, PAGE_SIZE, ct)
                                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!eventResponse.Success)
            {
                //If the request fails, we restore the previous page number in order to retry the same request next time
                eventsFetchData.pageNumber--;
                await inWorldWarningNotificationView.AnimatedShowAsync(FAILED_EVENTS_FETCHING_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct);
                return eventsFetchData.totalToFetch;
            }

            eventPlaceIds.Clear();

            foreach (var item in eventResponse.Value.data)
                eventPlaceIds.Add(item.placeId);

            Result<PlacesData.PlacesAPIResponse> placesResponse = await placesAPIService.GetPlacesByIdsAsync(eventPlaceIds, ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!placesResponse.Success)
            {
                //If the request fails, we restore the previous page number in order to retry the same request next time
                eventsFetchData.pageNumber--;
                await inWorldWarningNotificationView.AnimatedShowAsync(FAILED_EVENTS_PLACES_FETCHING_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct);
                return eventsFetchData.totalToFetch;
            }

            placeInfoCache.Clear();

            foreach (var place in placesResponse.Value.data)
                placeInfoCache.Add(place.id, place);

            foreach (var item in eventResponse.Value.data)
                eventsFetchData.items.Add(new PlaceAndEventDTO
                {
                    Place = placeInfoCache[item.placeId],
                    Event = item
                });

            return eventResponse.Value.total;
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
