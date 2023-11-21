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
        private readonly IPlacesAPIService placesAPIService;
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
            PlacesData.PlaceInfo placeInfo = await placesAPIService.GetPlace(parcel, cts.Token);
            view.placeName.text = placeInfo.title;
            view.placeCreator.text = $"created by <b>{placeInfo.contact_name}</b>";
            view.placeDescription.text = placeInfo.description;
            view.location.text = placeInfo.base_position;
            view.visits.text = placeInfo.user_visits.ToString();
            view.upvotes.text = placeInfo.like_rate_as_float != null ? $"{placeInfo.like_rate_as_float.Value * 100:0}%" : "-%";
            foreach (string placeInfoCategory in placeInfo.categories)
            {

            }
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
