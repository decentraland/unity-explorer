using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using UnityEngine;

namespace DCL.Navmap
{
    public class FloatingPanelController
    {
        private readonly FloatingPanelView view;
        private readonly MultiStateButtonController likeButtonController;
        private readonly MultiStateButtonController dislikeButtonController;
        private MultiStateButtonController favoriteButtonController;

        public FloatingPanelController(FloatingPanelView view)
        {
            this.view = view;
            view.closeButton.onClick.RemoveAllListeners();
            view.closeButton.onClick.AddListener(HidePanel);
            likeButtonController = new MultiStateButtonController(view.likeButton, true);
            dislikeButtonController = new MultiStateButtonController(view.dislikeButton, true);
            favoriteButtonController = new MultiStateButtonController(view.favoriteButton, true);
            likeButtonController.OnButtonClicked += OnLike;
            dislikeButtonController.OnButtonClicked += OnDislike;
            favoriteButtonController.OnButtonClicked += OnFavorite;
            view.gameObject.SetActive(false);
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

        public void ShowPanel()
        {
            view.rectTransform.localScale = Vector3.zero;
            view.gameObject.SetActive(true);
            view.rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.InCirc);
        }

        private void HidePanel()
        {
            view.rectTransform.localScale = Vector3.one;
            view.rectTransform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.OutCirc).OnComplete(()=>view.gameObject.SetActive(false));
        }

        private async UniTaskVoid AnimatePanelShow()
        {
        }
    }
}
