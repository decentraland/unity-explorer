using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace DCL.Navmap
{
    public class FloatingPanelController : IDisposable
    {
        private readonly FloatingPanelView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly Func<Vector2Int, CancellationToken, UniTask> teleportToParcel;

        private readonly Dictionary<string, GameObject> categoriesDictionary;

        private readonly Vector2 rectTransformLocalPosition = new Vector3(742, -32);
        private readonly Vector2 rectTransformLocalPositionOutside = new Vector3(1500, -32);
        private readonly ImageController placeImageController;

        private MultiStateButtonController likeButtonController;
        private MultiStateButtonController dislikeButtonController;
        private MultiStateButtonController favoriteButtonController;
        private CancellationTokenSource cts;


        public FloatingPanelController(FloatingPanelView view, IPlacesAPIService placesAPIService,
           IWebRequestController webRequestController, Func<Vector2Int, CancellationToken, UniTask> teleportToParcel)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;
            this.teleportToParcel = teleportToParcel;

            view.closeButton.onClick.RemoveAllListeners();
            view.closeButton.onClick.AddListener(HidePanel);
            view.gameObject.SetActive(false);
            placeImageController = new ImageController(view.placeImage, webRequestController);
            categoriesDictionary = new Dictionary<string, GameObject>();

            for (var i = 0; i < view.categories.Length; i++)
                categoriesDictionary.Add(view.categoryNames[i], view.categories[i]);

            ResetCategories();
            InitButtons();
        }

        public void Dispose()
        {
            likeButtonController.OnButtonClicked -= OnLike;
            dislikeButtonController.OnButtonClicked -= OnDislike;
            favoriteButtonController.OnButtonClicked -= OnFavorite;
        }

        private void InitButtons()
        {
            likeButtonController = new MultiStateButtonController(view.likeButton, true);
            dislikeButtonController = new MultiStateButtonController(view.dislikeButton, true);
            favoriteButtonController = new MultiStateButtonController(view.favoriteButton, true);
            likeButtonController.OnButtonClicked += OnLike;
            dislikeButtonController.OnButtonClicked += OnDislike;
            favoriteButtonController.OnButtonClicked += OnFavorite;
            view.backButton.onClick.AddListener(HideWithSlide);
        }

        private void HideWithSlide()
        {
            view.rectTransform.localPosition = rectTransformLocalPosition;
            view.rectTransform.DOLocalMove(rectTransformLocalPositionOutside, 0.5f).SetEase(Ease.Linear).OnComplete(() => view.gameObject.SetActive(false));
        }

        public void HandlePanelVisibility(Vector2Int parcel, bool popAnimation = true)
        {
            view.rectTransform.localPosition = rectTransformLocalPosition;

            if (view.gameObject.activeInHierarchy) { GetPlaceInfoAsync(parcel).Forget(); }
            else { ShowPanel(parcel, popAnimation); }
        }

        private void ShowPanel(Vector2Int parcel, bool popAnimation)
        {
            view.rectTransform.localScale = Vector3.zero;
            view.gameObject.SetActive(true);

            if (popAnimation)
            {
                view.rectTransform.localScale = Vector3.zero;
                view.rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.InCirc);
            }
            else
            {
                view.rectTransform.localScale = Vector3.one;
                view.rectTransform.localPosition = rectTransformLocalPositionOutside;
                view.rectTransform.DOLocalMove(rectTransformLocalPosition, 0.5f).SetEase(Ease.Linear);
            }

            cts = new CancellationTokenSource();
            GetPlaceInfoAsync(parcel).Forget();
        }

        private async UniTaskVoid GetPlaceInfoAsync(Vector2Int parcel)
        {
            try
            {
                view.jumpInButton.onClick.RemoveAllListeners();
                view.jumpInButton.onClick.AddListener(() => teleportToParcel(parcel, cts.Token).Forget());
                PlacesData.PlaceInfo placeInfo = await placesAPIService.GetPlaceAsync(parcel, cts.Token);
                ResetCategories();
                SetFloatingPanelInfo(placeInfo);
            }
            catch (Exception) { SetEmptyParcelInfo(parcel); }
        }

        private void SetEmptyParcelInfo(Vector2Int parcel)
        {
            view.placeName.text = "Empty parcel";
            view.placeCreator.text = "created by <b>Unknown</b>";
            view.placeDescription.text = "This place doesn't have a description set";
            view.location.text = parcel.ToString();
            view.visits.text = "-";
            view.upvotes.text = "-";
            view.parcelsCount.text = "1";

            ResetCategories();
        }

        private void SetFloatingPanelInfo(PlacesData.PlaceInfo placeInfo)
        {
            placeImageController.RequestImage(placeInfo.image);
            view.placeName.text = placeInfo.title;
            view.placeCreator.text = $"created by <b>{placeInfo.contact_name}</b>";

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

        private void HidePanel()
        {
            view.rectTransform.localScale = Vector3.one;
            view.rectTransform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.OutCirc).OnComplete(() => view.gameObject.SetActive(false));
        }
    }
}
