using DCL.Backpack.Gifting.Models;
using DG.Tweening;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Backpack.Gifting.Views
{
    public class GiftingItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float ANIMATION_TIME = 0.1f;
        private readonly Vector3 hoveredScale = new(1.05f, 1.05f, 1.05f);
        private CancellationTokenSource animationCts;

        public event Action<string>? OnItemSelected;

        [SerializeField] private GameObject container;

        [Header("State Containers")]
        [SerializeField] private GameObject loadingStateContainer;

        [SerializeField] private GameObject loadedStateContainer;

        [Header("UI Elements")]
        [SerializeField] private Image thumbnailImage;

        [SerializeField] public Image RarityBackground;
        [SerializeField] public Image FlapBackground;
        [SerializeField] public Image CategoryImage;
        

        [SerializeField] private GameObject selectionOutline;
        [SerializeField] private SkeletonLoadingView loadingView;

        private string currentUrn;

        private void OnDisable()
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            animationCts.SafeCancelAndDispose();
        }

        /// <summary>
        ///     This is the single entry point to configure the view's state from a ViewModel.
        /// </summary>
        public void Bind(IGiftableItemViewModel viewModel, bool isSelected)
        {
            transform.localScale = Vector3.one;
            
            currentUrn = viewModel.Urn;

            // Set selection state
            selectionOutline.SetActive(isSelected);

            // Set thumbnail/loading state
            switch (viewModel.ThumbnailState)
            {
                case ThumbnailState.NotLoaded:
                case ThumbnailState.Loading:
                    loadingStateContainer.SetActive(true);
                    loadedStateContainer.SetActive(false);
                    loadingView.ShowLoading();
                    break;

                case ThumbnailState.Loaded:
                    loadingStateContainer.SetActive(false);
                    loadedStateContainer.SetActive(true);
                    thumbnailImage.sprite = viewModel.Thumbnail;
                    loadingView.HideLoading();
                    break;

                case ThumbnailState.Error:
                    // For simplicity, we can treat error as a perpetual loading state for now
                    loadingStateContainer.SetActive(true);
                    loadedStateContainer.SetActive(false);
                    break;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();
            container.transform.DOScale(hoveredScale, ANIMATION_TIME)
                .SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: animationCts.Token);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();
            container.transform.DOScale(Vector3.one, ANIMATION_TIME)
                .SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: animationCts.Token);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (!string.IsNullOrEmpty(currentUrn))
                OnItemSelected?.Invoke(currentUrn);
        }
    }
}