using DCL.Backpack.Gifting.Models;
using DG.Tweening;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Backpack.Gifting.Views
{
    public readonly struct GiftItemStyleSnapshot
    {
        public readonly Sprite? categoryIcon;
        public readonly Sprite? rarityBackground;
        public readonly Color flapColor;

        public GiftItemStyleSnapshot(Sprite? categoryIcon, Sprite? rarityBackground, Color flapColor)
        {
            this.categoryIcon = categoryIcon;
            this.rarityBackground = rarityBackground;
            this.flapColor = flapColor;
        }
    }
    
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
        [SerializeField] private GameObject equippedContainer;

        [Header("UI Elements")]
        [SerializeField] private Image thumbnailImage;

        [SerializeField] public TextMeshProUGUI NftCount;
        [SerializeField] public GameObject NftCountCountainer;
        [SerializeField] public Image RarityBackground;
        [SerializeField] public Image FlapBackground;
        [SerializeField] public Image CategoryImage;
        

        [SerializeField] private GameObject selectionOutline;
        [SerializeField] private SkeletonLoadingView loadingView;

        private string currentUrn;

        private void OnDisable()
        {
            if (container != null)
            {
                container.transform.DOKill();
                container.transform.localScale = Vector3.one;
            }
            
            animationCts.SafeCancelAndDispose();
        }

        /// <summary>
        ///     This is the single entry point to configure the view's state from a ViewModel.
        /// </summary>
        public void Bind<T>(T viewModel, bool isSelected) where T : IGiftableItemViewModel
        {
            if (container != null)
            {
                container.transform.DOKill();
                container.transform.localScale = Vector3.one;
            }

            animationCts.SafeCancelAndDispose();
            animationCts = null;

            transform.localScale = Vector3.one;
            
            currentUrn = viewModel.Urn;

            equippedContainer.SetActive(viewModel is { IsEquipped: true, IsGiftable: false });
            selectionOutline.SetActive(isSelected);

            if (RarityBackground) RarityBackground.color = Color.white;
            
            switch (viewModel.ThumbnailState)
            {
                case ThumbnailState.NotLoaded:
                case ThumbnailState.Loading:
                case ThumbnailState.Error:
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