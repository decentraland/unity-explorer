using Cysharp.Threading.Tasks;
using DCLServices.PlacesAPIService;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Navmap
{
    public class FloatingPanelController : IDisposable
    {
        private readonly FloatingPanelView view;
        private readonly IPlacesAPIClient placesAPIClient;
        private MultiStateButtonController likeButtonController;
        private MultiStateButtonController dislikeButtonController;
        private MultiStateButtonController favoriteButtonController;

        private CancellationTokenSource cts;

        public FloatingPanelController(FloatingPanelView view, IPlacesAPIClient placesAPIClient)
        {
            this.view = view;
            this.placesAPIClient = placesAPIClient;

            view.closeButton.onClick.RemoveAllListeners();
            view.closeButton.onClick.AddListener(HidePanel);
            view.gameObject.SetActive(false);
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

        public void ShowPanel(Vector2Int parcel)
        {
            view.rectTransform.localScale = Vector3.zero;
            view.gameObject.SetActive(true);
            view.rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.InCirc);
            cts = new CancellationTokenSource();
            GetPlaceInfo(parcel).Forget();
        }

        private async UniTaskVoid GetPlaceInfo(Vector2Int parcel)
        {
            PlacesData.PlaceInfo placeInfo = await placesAPIClient.GetPlace(parcel, cts.Token);
            view.placeName.text = placeInfo.title;
            view.placeCreator.text = $"created by <b>{placeInfo.owner}</b>";
            view.placeDescription.text = placeInfo.description;
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
