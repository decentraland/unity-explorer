using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using UnityEngine;

namespace DCL.Navmap
{
    public class EventInfoCardController : IDisposable
    {
        private const string ORIGIN = "jump in";
        private static readonly Vector2Int DEFAULT_DESTINATION_PARCEL = new (-9999, 9999);

        private readonly EventInfoCardView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly Dictionary<string, GameObject> categoriesDictionary;
        private readonly ImageController placeImageController;
        private readonly ImageController mapPinPlaceImageController;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly NavmapZoomController zoomController;
        private readonly INavmapBus navmapBus;
        private readonly IChatMessagesBus chatMessagesBus;

        private MultiStateButtonController likeButtonController;
        private MultiStateButtonController dislikeButtonController;
        private MultiStateButtonController favoriteButtonController;
        private CancellationTokenSource cts;
        private Vector2Int destination = DEFAULT_DESTINATION_PARCEL;
        private PlacesData.PlaceInfo? currentParcelPlaceInfo;
        private Vector2Int? currentParcel;

        public EventInfoCardController(
            EventInfoCardView view,
            IPlacesAPIService placesAPIService,
            IWebRequestController webRequestController,
            IMapPathEventBus mapPathEventBus,
            IChatMessagesBus chatMessagesBus,
            NavmapZoomController zoomController,
            INavmapBus navmapBus)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;
            this.mapPathEventBus = mapPathEventBus;
            this.chatMessagesBus = chatMessagesBus;
            this.zoomController = zoomController;
            this.navmapBus = navmapBus;

            view.closeButton.onClick.AddListener(Hide);
            view.mapPinCloseButton.onClick.AddListener(Hide);
            view.CanvasGroup.interactable = false;
            view.CanvasGroup.blocksRaycasts = false;
            placeImageController = new ImageController(view.placeImage, webRequestController);
            mapPinPlaceImageController = new ImageController(view.MapPinPlaceImage, webRequestController);
            categoriesDictionary = new Dictionary<string, GameObject>();

            for (var i = 0; i < view.categories.Length; i++)
                categoriesDictionary.Add(view.categoryNames[i], view.categories[i]);

            ResetCategories();
            InitButtons();
            this.mapPathEventBus.OnRemovedDestination += RemoveDestination;

            view.onPointerEnterAction += NavmapBlockZoom;
            view.onPointerExitAction += NavmapUnblockZoom;
        }

        public void Dispose()
        {
            likeButtonController.OnButtonClicked -= OnLike;
            dislikeButtonController.OnButtonClicked -= OnDislike;
            favoriteButtonController.OnButtonClicked -= OnFavorite;

            view.onPointerEnterAction -= NavmapBlockZoom;
            view.onPointerExitAction -= NavmapUnblockZoom;
        }

        public void Show(Vector2Int parcel, IPinMarker? pinMarker)
        {
            currentParcel = parcel;
            view.backButton.gameObject.SetActive(true);
            view.PlaceSection.gameObject.SetActive(pinMarker == null);
            view.MapPinSection.gameObject.SetActive(pinMarker != null);

            bool parcelIsDestination = destination == parcel;
            SetupDestinationButtons(parcelIsDestination);

            if (pinMarker != null)
            {
                view.MapPinTitle.text = pinMarker.Title;
                view.MapPinDescription.text = pinMarker.Description;
            }

            Show(parcel);
        }

        public void Hide()
        {
            view.gameObject.SetActive(false);
            view.CanvasGroup.interactable = false;
            view.CanvasGroup.blocksRaycasts = false;
        }

        private void InitButtons()
        {
            likeButtonController = new MultiStateButtonController(view.likeButton, true);
            dislikeButtonController = new MultiStateButtonController(view.dislikeButton, true);
            favoriteButtonController = new MultiStateButtonController(view.favoriteButton, true);
            likeButtonController.OnButtonClicked += OnLike;
            dislikeButtonController.OnButtonClicked += OnDislike;
            favoriteButtonController.OnButtonClicked += OnFavorite;
            view.backButton.onClick.AddListener(HidePanelFromBackButton);
            view.setAsDestinationButton.onClick.RemoveAllListeners();
            view.setAsDestinationButton.onClick.AddListener(SetAsDestination);
            view.setAsDestinationMapPinButton.onClick.RemoveAllListeners();
            view.setAsDestinationMapPinButton.onClick.AddListener(SetAsDestination);
            view.removeDestinationButton.onClick.RemoveAllListeners();
            view.removeDestinationButton.onClick.AddListener(OnRemoveDestinationButtonClicked);
            view.removeMapPinDestinationButton.onClick.RemoveAllListeners();
            view.removeMapPinDestinationButton.onClick.AddListener(OnRemoveDestinationButtonClicked);
            view.jumpInButton.onClick.RemoveAllListeners();
            view.jumpInButton.onClick.AddListener(JumpIn);
        }

        private void Show(Vector2Int parcel)
        {
            view.gameObject.SetActive(true);
            view.CanvasGroup.interactable = true;
            view.CanvasGroup.blocksRaycasts = true;
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.OnShowAudio);

            cts = new CancellationTokenSource();
            FetchAndSetupPlaceInfo(parcel).Forget();
        }

        private async UniTaskVoid FetchAndSetupPlaceInfo(Vector2Int parcel)
        {
            try
            {
                currentParcelPlaceInfo = await placesAPIService.GetPlaceAsync(parcel, cts.Token);
                ResetCategories();

                if (currentParcelPlaceInfo == null)
                    SetEmptyParcelInfo(parcel);
                else
                    Set(currentParcelPlaceInfo);
            }
            catch (Exception) { SetEmptyParcelInfo(parcel); }
        }

        private void OnRemoveDestinationButtonClicked()
        {
            mapPathEventBus.RemoveDestination();
        }

        private void RemoveDestination()
        {
            destination = DEFAULT_DESTINATION_PARCEL;
            SetupDestinationButtons(parcelIsDestination: false);
        }

        private void SetAsDestination()
        {
            if (currentParcel == null || currentParcelPlaceInfo == null) return;

            destination = currentParcel!.Value;
            SetupDestinationButtons(parcelIsDestination: true);
            navmapBus.SelectDestination(currentParcelPlaceInfo!);
        }

        private void SetupDestinationButtons(bool parcelIsDestination)
        {
            view.setAsDestinationButton.gameObject.SetActive(!parcelIsDestination);
            view.setAsDestinationMapPinButton.gameObject.SetActive(!parcelIsDestination);
            view.removeMapPinDestinationButton.gameObject.SetActive(parcelIsDestination);
            view.removeDestinationButton.gameObject.SetActive(parcelIsDestination);
        }

        private void JumpIn()
        {
            if (currentParcel == null) return;
            if (currentParcelPlaceInfo == null) return;

            if (destination == currentParcel)
                mapPathEventBus.ArrivedToDestination();

            navmapBus.JumpIn(currentParcelPlaceInfo!);
            chatMessagesBus.Send($"/{ChatCommandsUtils.COMMAND_GOTO} {currentParcel?.x},{currentParcel?.y}", ORIGIN);
        }

        private void SetEmptyParcelInfo(Vector2Int parcel)
        {
            view.placeName.text = "Empty parcel";
            view.placeCreator.gameObject.SetActive(false);
            view.placeDescription.text = "This place doesn't have a description set";
            view.location.text = parcel.ToString().Replace("(", "").Replace(")", "");
            view.visits.text = "-";
            view.upvotes.text = "-";
            view.parcelsCount.text = "1";
            placeImageController.SetVisible(false);
            mapPinPlaceImageController.SetVisible(false);

            ResetCategories();
        }

        private void Set(PlacesData.PlaceInfo placeInfo)
        {
            if (view.PlaceSection.activeInHierarchy)
            {
                placeImageController.SetVisible(true);
                placeImageController.RequestImage(placeInfo.image);
            }

            if (view.MapPinSection.activeInHierarchy)
            {
                mapPinPlaceImageController.SetVisible(true);
                mapPinPlaceImageController.RequestImage(placeInfo.image);
            }

            view.placeName.text = placeInfo.title;
            view.placeCreator.text = $"created by <b>{placeInfo.contact_name}</b>";
            view.placeCreator.gameObject.SetActive(!string.IsNullOrEmpty(placeInfo.contact_name));

            view.placeDescription.text = string.IsNullOrEmpty(placeInfo.description)
                ? "This place doesn't have a description set"
                : placeInfo.description;

            view.location.text = placeInfo.base_position;
            view.visits.SetText("{0:0}", placeInfo.user_visits);
            view.parcelsCount.SetText("{0:0}", placeInfo.Positions.Length);

            SetUpVotes(placeInfo);

            if (placeInfo.categories.Length == 0)
            {
                view.appearsIn.SetActive(false);
                return;
            }

            var hasVisibleCategories = false;

            foreach (string placeInfoCategory in placeInfo.categories)
                if (categoriesDictionary.TryGetValue(placeInfoCategory, out GameObject categoryGameObject))
                {
                    hasVisibleCategories = true;
                    categoryGameObject.SetActive(true);
                }

            view.appearsIn.SetActive(hasVisibleCategories);
        }

        private void SetUpVotes(PlacesData.PlaceInfo placeInfo)
        {
            string likeRate = placeInfo.like_rate;

            if (string.IsNullOrEmpty(likeRate) || !float.TryParse(likeRate, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                view.upvotes.SetText("-%");
            else
                view.upvotes.SetText("{0:0}%", result * 100);
        }

        private void ResetCategories()
        {
            foreach (KeyValuePair<string, GameObject> keyValuePair in categoriesDictionary)
                keyValuePair.Value.SetActive(false);
        }

        private void OnFavorite(bool isFavorite) { }

        private void OnDislike(bool isDisliked)
        {
            if (isDisliked)
                likeButtonController.SetButtonState(false);
        }

        private void OnLike(bool isLiked)
        {
            if (isLiked)
                dislikeButtonController.SetButtonState(false);
        }

        private void HidePanelFromBackButton()
        {
            view.panelAnimator.SetTrigger(UIAnimationHashes.TO_RIGHT);
            view.CanvasGroup.interactable = false;
            view.CanvasGroup.blocksRaycasts = false;
        }

        private void NavmapBlockZoom() =>
            zoomController.SetBlockZoom(true);

        private void NavmapUnblockZoom() =>
            zoomController.SetBlockZoom(false);
    }
}
