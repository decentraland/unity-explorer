using Cysharp.Threading.Tasks;
using DCLServices.PlacesAPIService;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class FloatingPanelController : IDisposable
    {
        private readonly FloatingPanelView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly Dictionary<string, GameObject> categoriesDictionary;

        private MultiStateButtonController likeButtonController;
        private MultiStateButtonController dislikeButtonController;
        private MultiStateButtonController favoriteButtonController;

        private CancellationTokenSource cts;

        public FloatingPanelController(FloatingPanelView view, IPlacesAPIService placesAPIService)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;

            view.closeButton.onClick.RemoveAllListeners();
            view.closeButton.onClick.AddListener(HidePanel);
            view.gameObject.SetActive(false);

            categoriesDictionary = new Dictionary<string, GameObject>();
            for (var i = 0; i < view.categories.Length; i++)
                categoriesDictionary.Add(view.categoryNames[i], view.categories[i]);

            ResetCategories();
            InitButtons();
        }

        private void InitButtons()
        {
            likeButtonController = new MultiStateButtonController(view.likeButton, true);
            dislikeButtonController = new MultiStateButtonController(view.dislikeButton, true);
            favoriteButtonController = new MultiStateButtonController(view.favoriteButton, true);
            likeButtonController.OnButtonClicked += OnLike;
            dislikeButtonController.OnButtonClicked += OnDislike;
            favoriteButtonController.OnButtonClicked += OnFavorite;
        }

        public void HandlePanelVisibility(Vector2Int parcel)
        {
            if (view.gameObject.activeInHierarchy)
            {
                GetPlaceInfoAsync(parcel).Forget();
            }
            else
            {
                ShowPanel(parcel);
            }
        }

        private void ShowPanel(Vector2Int parcel)
        {
            view.rectTransform.localScale = Vector3.zero;
            view.gameObject.SetActive(true);
            view.rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.InCirc);
            cts = new CancellationTokenSource();
            GetPlaceInfoAsync(parcel).Forget();
        }

        private async UniTaskVoid GetPlaceInfoAsync(Vector2Int parcel)
        {
            try
            {
                PlacesData.PlaceInfo placeInfo = await placesAPIService.GetPlace(parcel, cts.Token);
                ResetCategories();
                SetFloatingPanelInfo(placeInfo);
            }
            catch (Exception ex)
            {
                SetEmptyParcelInfo(parcel);
            }
        }

        private void SetEmptyParcelInfo(Vector2Int parcel)
        {
            view.placeName.text = "Empty parcel";
            view.placeCreator.text = $"created by <b>Unknown</b>";
            view.placeDescription.text = "This place doesn't have a description set";
            view.location.text = parcel.ToString();
            view.visits.text = "-";
            view.upvotes.text = "-";
            view.parcelsCount.text = "1";

            ResetCategories();
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.contentViewport);
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.descriptionContent);
        }

        private void SetFloatingPanelInfo(PlacesData.PlaceInfo placeInfo)
        {
            view.placeName.text = placeInfo.title;
            view.placeCreator.text = $"created by <b>{placeInfo.contact_name}</b>";
            view.placeDescription.text = string.IsNullOrEmpty(placeInfo.description)
                ? "This place doesn't have a description set"
                : placeInfo.description;
            view.location.text = placeInfo.base_position;
            view.visits.text = placeInfo.user_visits.ToString();
            view.upvotes.text = placeInfo.like_rate_as_float != null ? $"{placeInfo.like_rate_as_float.Value * 100:0}%" : "-%";
            view.parcelsCount.text = placeInfo.Positions.Length.ToString();

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.contentViewport);
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.descriptionContent);

            if (placeInfo.categories.Length == 0)
                return;

            foreach (string placeInfoCategory in placeInfo.categories)
                if (categoriesDictionary.TryGetValue(placeInfoCategory, out GameObject categoryGameObject))
                    categoryGameObject.SetActive(true);

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.CategoriesContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.descriptionContent);
        }

        private void ResetCategories()
        {
            foreach (KeyValuePair<string, GameObject> keyValuePair in categoriesDictionary)
                keyValuePair.Value.SetActive(false);

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.CategoriesContainer);
        }

        private void OnFavorite(bool isFavorite)
        {
        }

        private void OnDislike(bool isDisliked)
        {
            if(isDisliked)
                likeButtonController.SetButtonState(false);
        }

        private void OnLike(bool isLiked)
        {
            if(isLiked)
                dislikeButtonController.SetButtonState(false);
        }

        private void HidePanel()
        {
            view.rectTransform.localScale = Vector3.one;
            view.rectTransform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.OutCirc).OnComplete(()=>view.gameObject.SetActive(false));
        }

        public void Dispose()
        {
            likeButtonController.OnButtonClicked -= OnLike;
            dislikeButtonController.OnButtonClicked -= OnDislike;
            favoriteButtonController.OnButtonClicked -= OnFavorite;
        }
    }
}
