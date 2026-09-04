using DCL.UI;
using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Shop
{
    public class ShopCarouselView : MonoBehaviour
    {
        private const int MAX_DOTS = 6;
        private const int DOTS_POOL_CAPACITY = 6;
        private const float PAGE_TWEEN_DURATION = 0.3f;
        private const float DOTS_ANIMATION_DURATION = 0.3f;

        [field: SerializeField] public TMP_Text Title { get; private set; } = null!;
        [field: SerializeField] public Button? ViewAllButton { get; private set; }
        [field: SerializeField] public RectTransform Viewport { get; private set; } = null!;
        [field: SerializeField] public RectTransform Track { get; private set; } = null!;
        [field: SerializeField] public HorizontalLayoutGroup TrackLayout { get; private set; } = null!;
        [field: SerializeField] public Button LeftArrow { get; private set; } = null!;
        [field: SerializeField] public Button RightArrow { get; private set; } = null!;
        [field: SerializeField] public Transform DotsContainer { get; private set; } = null!;
        [field: SerializeField] public Button DotPrefab { get; private set; } = null!;
        [field: SerializeField] public Color SelectedDotColor { get; private set; } = Color.white;
        [field: SerializeField] public Color NonSelectedDotColor { get; private set; } = new (0f, 0f, 0f, 0.5f);
        [field: SerializeField] public float SelectedDotWidth { get; private set; } = 24f;
        [field: SerializeField] public float NonSelectedDotWidth { get; private set; } = 8f;
        [field: SerializeField] public float CardWidth { get; private set; } = 232f;
        [field: SerializeField] public SkeletonLoadingView? Skeleton { get; private set; }
        [field: SerializeField] public GameObject ControlsRoot { get; private set; } = null!;

        private readonly List<Button> dots = new ();
        private IObjectPool<Button> dotsPool = null!;
        private Tweener? trackTween;

        public int ItemCount { get; private set; }

        public int CardsPerPage { get; private set; } = 1;

        public int PageCount { get; private set; }

        public int CurrentPage { get; private set; }

        public event Action? ViewAllClicked;
        public event Action<int>? PageChanged;

        private void Awake()
        {
            dotsPool = new ObjectPool<Button>(
                InstantiateDot,
                defaultCapacity: DOTS_POOL_CAPACITY,
                actionOnGet: dot =>
                {
                    dot.gameObject.SetActive(true);
                    dot.transform.SetAsLastSibling();
                },
                actionOnRelease: dot => dot.gameObject.SetActive(false));

            LeftArrow.onClick.AddListener(() => GoToPage(CurrentPage - 1));
            RightArrow.onClick.AddListener(() => GoToPage(CurrentPage + 1));
            ViewAllButton?.onClick.AddListener(() => ViewAllClicked?.Invoke());
        }

        private void OnDisable() =>
            trackTween?.Kill();

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled || ItemCount == 0)
                return;

            Recalculate();
            GoToPage(CurrentPage, animate: false);
        }

        public void SetLoading(bool loading)
        {
            if (Skeleton != null)
            {
                if (loading)
                    Skeleton.ShowLoading();
                else
                    Skeleton.HideLoading();
            }

            ControlsRoot.SetActive(!loading);
        }

        public void SetItemCount(int count)
        {
            ItemCount = count;
            Recalculate();
            GoToPage(0, animate: false);
        }

        public void GoToPage(int page, bool animate = true)
        {
            if (PageCount == 0)
            {
                CurrentPage = 0;
                Track.anchoredPosition = new Vector2(0f, Track.anchoredPosition.y);
                UpdateControls();
                return;
            }

            CurrentPage = Mathf.Clamp(page, 0, PageCount - 1);
            float targetX = -CurrentPage * CardsPerPage * (CardWidth + TrackLayout.spacing);

            trackTween?.Kill();

            if (animate)
                trackTween = Track.DOAnchorPosX(targetX, PAGE_TWEEN_DURATION).SetEase(Ease.OutCubic);
            else
                Track.anchoredPosition = new Vector2(targetX, Track.anchoredPosition.y);

            UpdateControls();
            PageChanged?.Invoke(CurrentPage);
        }

        private void Recalculate()
        {
            float spacing = TrackLayout.spacing;
            float viewportWidth = Viewport.rect.width;
            CardsPerPage = Mathf.Max(1, Mathf.FloorToInt((viewportWidth + spacing) / (CardWidth + spacing)));
            PageCount = ItemCount == 0 ? 0 : Mathf.CeilToInt(ItemCount / (float)CardsPerPage);
            RebuildDots();
        }

        private void RebuildDots()
        {
            foreach (Button dot in dots)
                dotsPool.Release(dot);

            dots.Clear();
            int dotCount = Mathf.Min(PageCount, MAX_DOTS);

            for (var i = 0; i < dotCount; i++)
            {
                Button dot = dotsPool.Get();
                int dotIndex = i;
                dot.onClick.RemoveAllListeners();
                dot.onClick.AddListener(() => GoToPage(PageForDot(dotIndex)));
                dots.Add(dot);
            }
        }

        private void UpdateControls()
        {
            bool paged = PageCount > 1;
            LeftArrow.gameObject.SetActive(paged);
            RightArrow.gameObject.SetActive(paged);
            LeftArrow.interactable = CurrentPage > 0;
            RightArrow.interactable = CurrentPage < PageCount - 1;
            DotsContainer.gameObject.SetActive(paged);

            int selectedDot = DotForPage(CurrentPage);

            for (var i = 0; i < dots.Count; i++)
                AnimateDot(dots[i], i == selectedDot);
        }

        private int DotForPage(int page) =>
            PageCount <= MAX_DOTS || PageCount <= 1 ? page : Mathf.RoundToInt(page * (MAX_DOTS - 1) / (float)(PageCount - 1));

        private int PageForDot(int dotIndex) =>
            PageCount <= MAX_DOTS ? dotIndex : Mathf.RoundToInt(dotIndex * (PageCount - 1) / (float)(MAX_DOTS - 1));

        private void AnimateDot(Button dot, bool isSelected)
        {
            dot.image.color = isSelected ? SelectedDotColor : NonSelectedDotColor;
            var rectTransform = (RectTransform)dot.transform;

            DOTween.To(
                        () => rectTransform.rect.width,
                        width => rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width),
                        isSelected ? SelectedDotWidth : NonSelectedDotWidth,
                        DOTS_ANIMATION_DURATION)
                   .SetEase(Ease.OutCubic);
        }

        private Button InstantiateDot() =>
            Instantiate(DotPrefab, DotsContainer);
    }
}
