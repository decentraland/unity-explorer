using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using DCL.EventsApi;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Navmap
{
    public class PlaceInfoPanelController
    {
        private readonly PlaceInfoPanelView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly INavmapBus navmapBus;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IEventsApiService eventsApiService;
        private readonly ObjectPool<EventElementView> eventElementPool ;
        private readonly SharePlacesAndEventsContextMenuController shareContextMenu;
        private readonly IWebBrowser webBrowser;
        private readonly ImageController thumbnailImage;
        private readonly MultiStateButtonController dislikeButton;
        private readonly MultiStateButtonController likeButton;
        private readonly MultiStateButtonController favoriteButton;
        private readonly List<EventElementView> eventElements = new ();
        private PlacesData.PlaceInfo? place;
        private CancellationTokenSource? favoriteCancellationToken;
        private CancellationTokenSource? rateCancellationToken;
        private CancellationTokenSource? fetchEventsCancellationToken;
        private CancellationTokenSource? attendEventCancellationToken;
        private CancellationTokenSource? openEventDetailsCancellationToken;
        private Vector2Int? currentBaseParcel;
        private Vector2Int? destination;

        public PlaceInfoPanelController(PlaceInfoPanelView view,
            IWebRequestController webRequestController,
            IPlacesAPIService placesAPIService,
            IMapPathEventBus mapPathEventBus,
            INavmapBus navmapBus,
            IChatMessagesBus chatMessagesBus,
            IEventsApiService eventsApiService,
            ObjectPool<EventElementView> eventElementPool,
            SharePlacesAndEventsContextMenuController shareContextMenu,
            IWebBrowser webBrowser)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;
            this.mapPathEventBus = mapPathEventBus;
            this.navmapBus = navmapBus;
            this.chatMessagesBus = chatMessagesBus;
            this.eventsApiService = eventsApiService;
            this.eventElementPool = eventElementPool;
            this.shareContextMenu = shareContextMenu;
            this.webBrowser = webBrowser;
            thumbnailImage = new ImageController(view.Thumbnail, webRequestController);

            mapPathEventBus.OnSetDestination += SetDestination;
            mapPathEventBus.OnRemovedDestination += RemoveDestination;

            view.EventsTabButton.onClick.AddListener(() =>
            {
                Toggle(Section.EVENTS);
                FetchAndShowEventsOfThePlace();
            });

            view.PhotosTabButton.onClick.AddListener(() =>
            {
                Toggle(Section.PHOTOS);
                FetchPhotos();
            });

            view.OverviewTabButton.onClick.AddListener(() => Toggle(Section.OVERVIEW));

            dislikeButton = new MultiStateButtonController(view.DislikeButton, true);
            dislikeButton.OnButtonClicked += OnDislikeButtonClick;

            likeButton = new MultiStateButtonController(view.LikeButton, true);
            likeButton.OnButtonClicked += OnLikeButtonClick;

            favoriteButton = new MultiStateButtonController(view.FavoriteButton, true);
            favoriteButton.OnButtonClicked += SetAsFavorite;

            view.ShareButton.onClick.AddListener(Share);
            view.SetAsHomeButton.onClick.AddListener(SetAsHome);
            view.JumpInButton.onClick.AddListener(JumpIn);
            view.StartNavigationButton.onClick.AddListener(StartNavigation);
            view.StopNavigationButton.onClick.AddListener(StopNavigation);
        }

        public void Show()
        {
            view.gameObject.SetActive(true);
        }

        public void Hide()
        {
            view.gameObject.SetActive(false);
        }

        public void Set(PlacesData.PlaceInfo place)
        {
            this.place = place;

            if (VectorUtilities.TryParseVector2Int(place.base_position, out Vector2Int result))
                currentBaseParcel = result;
            else
                currentBaseParcel = null;

            thumbnailImage.RequestImage(place.image);
            view.PlaceNameLabel.text = place.title;
            view.CreatorNameLabel.text = $"created by <b>{place.contact_name}</b>";
            view.LikeRateLabel.text = $"{(place.like_rate_as_float ?? 0) * 100:F0}%";
            view.PlayerCountLabel.text = place.user_count.ToString();
            view.DescriptionLabel.text = place.description;
            view.DescriptionLabel.ConvertUrlsToClickeableLinks(OpenUrl);
            view.CoordinatesLabel.text = place.base_position;
            view.ParcelCountLabel.text = place.Positions.Length.ToString();
            view.AppearsOnContainer.SetActive(place.categories.Length > 0);

            likeButton.SetButtonState(place.user_like);
            dislikeButton.SetButtonState(place.user_dislike);
            favoriteButton.SetButtonState(place.user_favorite);

            SetCategories(place);

            UpdateDestinationStatus();
            ClearEventElements();
        }

        public void SetLiveEvent(EventDTO @event)
        {
            view.LiveEventContainer.SetActive(true);
            view.LiveEventNameLabel.text = @event.name;
        }

        public void HideLiveEvent()
        {
            view.LiveEventContainer.SetActive(false);
        }

        public void Toggle(Section section)
        {
            view.EventsTabContainer.SetActive(section == Section.EVENTS);
            view.EventsTabSelected.SetActive(section == Section.EVENTS);
            view.OverviewTabContainer.SetActive(section == Section.OVERVIEW);
            view.OverviewTabSelected.SetActive(section == Section.OVERVIEW);
            view.PhotosTabContainer.SetActive(section == Section.PHOTOS);
            view.PhotosTabSelected.SetActive(section == Section.PHOTOS);
        }

        private void SetCategories(PlacesData.PlaceInfo place)
        {
            foreach (PlaceInfoPanelView.AppearsOnCategory appearsOnCategory in view.AppearsOnCategories)
                appearsOnCategory.container.SetActive(false);

            foreach (string category in place.categories)
            foreach (PlaceInfoPanelView.AppearsOnCategory appearsOnCategory in view.AppearsOnCategories)
                if (appearsOnCategory.category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    appearsOnCategory.container.SetActive(true);
        }

        private void SetAsFavorite(bool isFavorite)
        {
            favoriteCancellationToken = favoriteCancellationToken.SafeRestart();
            SetAsFavoriteAsync(favoriteCancellationToken.Token).Forget();
            return;

            async UniTaskVoid SetAsFavoriteAsync(CancellationToken ct)
            {
                await placesAPIService.SetPlaceFavoriteAsync(place!.id, isFavorite, ct);
                favoriteButton.SetButtonState(isFavorite);
            }
        }

        private void StartNavigation()
        {
            if (place == null) return;
            navmapBus.SelectDestination(place);
        }

        private void StopNavigation()
        {
            mapPathEventBus.RemoveDestination();
        }

        private void RemoveDestination()
        {
            destination = null;
            UpdateDestinationStatus();
        }

        private void SetDestination(Vector2Int parcel, IPinMarker? arg2)
        {
            destination = parcel;
            UpdateDestinationStatus();
        }

        private void UpdateDestinationStatus()
        {
            bool isDestinationThisPlace = destination == currentBaseParcel;
            view.StartNavigationButton.gameObject.SetActive(!isDestinationThisPlace);
            view.StopNavigationButton.gameObject.SetActive(isDestinationThisPlace);
        }

        private void SetAsHome()
        {
            // TODO
        }

        private void JumpIn()
        {
            if (destination == currentBaseParcel)
                mapPathEventBus.ArrivedToDestination();

            navmapBus.JumpIn(place!);
            chatMessagesBus.Send($"/{ChatCommandsUtils.COMMAND_GOTO} {currentBaseParcel?.x},{currentBaseParcel?.y}", "jump in");
        }

        private void Share()
        {
            shareContextMenu.Set(place!);
            shareContextMenu.Show(view.SharePivot);
        }

        private void OpenUrl(string url) =>
            webBrowser.OpenUrl(url);

        private void OnLikeButtonClick(bool isEnabled)
        {
            rateCancellationToken = rateCancellationToken.SafeRestart();
            RateAsync(rateCancellationToken.Token).Forget();
            return;

            async UniTaskVoid RateAsync(CancellationToken ct)
            {
                await placesAPIService.RatePlaceAsync(isEnabled ? true : null, place!.id, ct);
                likeButton.SetButtonState(isEnabled);
                dislikeButton.SetButtonState(false);
            }
        }

        private void OnDislikeButtonClick(bool isEnabled)
        {
            rateCancellationToken = rateCancellationToken.SafeRestart();
            RateAsync(rateCancellationToken.Token).Forget();
            return;

            async UniTaskVoid RateAsync(CancellationToken ct)
            {
                await placesAPIService.RatePlaceAsync(isEnabled ? false : null, place!.id, ct);
                likeButton.SetButtonState(false);
                dislikeButton.SetButtonState(isEnabled);
            }
        }

        private void FetchAndShowEventsOfThePlace()
        {
            fetchEventsCancellationToken = fetchEventsCancellationToken.SafeRestart();
            FetchEventsAndShowThemAsync(fetchEventsCancellationToken.Token).Forget();

            return;

            async UniTaskVoid FetchEventsAndShowThemAsync(CancellationToken ct)
            {
                view.EmptyEventsContainer.SetActive(false);

                SetAsLoadingState();

                IReadOnlyList<EventDTO> events = await eventsApiService.GetEventsByParcelAsync(place!.Positions, ct);

                ClearEventElements();

                view.EmptyEventsContainer.SetActive(events.Count == 0);

                foreach (EventDTO @event in events)
                {
                    EventElementView element = eventElementPool.Get();
                    eventElements.Add(element);

                    var schedule = "";

                    if (DateTime.TryParse(@event.start_at, null, DateTimeStyles.RoundtripKind, out DateTime startAt))
                    {
                        schedule = @event.live
                            ? $"Event started {(DateTime.UtcNow - startAt).TotalMinutes} min ago"
                            // TODO: we might need to convert to local, currently R:RFC1123 Fri, 18 Apr 2008 20:30:00 GMT
                            : startAt.ToString("R");
                    }

                    element.InterestedButton!.OnButtonClicked += interested =>
                    {
                        attendEventCancellationToken = attendEventCancellationToken.SafeRestart();
                        SetAsInterestedAsync(interested, @event, element, attendEventCancellationToken.Token).Forget();
                    };
                    element.ShowDetailsButton.onClick.AddListener(() => OpenEventDetails(@event));
                    element.ShareButton.onClick.AddListener(() => Share(@event, element));
                    element.Thumbnail?.RequestImage(@event.image, true);
                    element.LiveContainer.SetActive(@event.live);
                    element.EventNameLabel.text = @event.name;
                    element.InterestedUserCountLabel.text = @event.total_attendees.ToString();
                    element.JoinedUserCountLabel.text = place.user_count.ToString();
                    element.ScheduleLabel.text = schedule;
                    element.Animator.SetTrigger(UIAnimationHashes.LOADED);
                }

                view.TabsLayoutRoot.ForceUpdateLayoutAsync(CancellationToken.None).Forget();
            }

            async UniTaskVoid SetAsInterestedAsync(bool interested, EventDTO @event,
                EventElementView element, CancellationToken ct)
            {
                if (interested)
                    await eventsApiService.MarkAsInterestedAsync(@event.id, ct);
                else
                    await eventsApiService.MarkAsNotInterestedAsync(@event.id, ct);

                element.InterestedButton!.SetButtonState(interested);
            }

            void SetAsLoadingState()
            {
                for (var i = 0; i < 8; i++)
                {
                    EventElementView element = eventElementPool.Get();
                    eventElements.Add(element);
                }
            }

            void Share(EventDTO @event, EventElementView element)
            {
                shareContextMenu.Set(@event);
                shareContextMenu.Show(element.SharePivot);
            }

            void OpenEventDetails(EventDTO @event)
            {
                openEventDetailsCancellationToken = openEventDetailsCancellationToken.SafeRestart();
                navmapBus.SelectEventAsync(@event, openEventDetailsCancellationToken.Token, place).Forget();
            }
        }

        private void ClearEventElements()
        {
            foreach (EventElementView element in eventElements)
            {
                element.ShareButton.onClick.RemoveAllListeners();
                element.ShowDetailsButton.onClick.RemoveAllListeners();
                element.InterestedButton?.ClearClickListeners();
                element.Animator.Rebind();
                element.Animator.Update(0f);
                element.Thumbnail?.StopLoading();
                eventElementPool.Release(element);
            }

            eventElements.Clear();
        }

        private void FetchPhotos()
        {
            // TODO
        }

        public enum Section
        {
            OVERVIEW,
            PHOTOS,
            EVENTS,
        }
    }
}