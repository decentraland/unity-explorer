using DCL.UI;
using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

// Optional serialized references: Unity writes the backing field, so the private setter has no caller
// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace DCL.Shop
{
    public class ShopCarouselView : MonoBehaviour
    {
        private const int MAX_DOTS = 6;
        private const int DOTS_POOL_CAPACITY = 6;
        private const float PAGE_TWEEN_DURATION = 0.3f;
        private const float MIN_SPACING = 8f;

        [field: SerializeField] public TMP_Text Title { get; private set; } = null!;
        [field: SerializeField] public Button? ViewAllButton { get; private set; }
        [field: SerializeField] public RectTransform Viewport { get; private set; } = null!;
        [field: SerializeField] public RectTransform Track { get; private set; } = null!;
        [field: SerializeField] public HorizontalLayoutGroup TrackLayout { get; private set; } = null!;
        [field: SerializeField] public Transform DotsContainer { get; private set; } = null!;
        [field: SerializeField] public Button DotPrefab { get; private set; } = null!;
        [field: SerializeField] public RectTransform SelectedDot { get; private set; } = null!;
        [field: SerializeField] public SkeletonLoadingView? Skeleton { get; private set; }
        [field: SerializeField] public GameObject ControlsRoot { get; private set; } = null!;
        [field: SerializeField] public LayoutElement RootLayout { get; private set; } = null!;
        [field: SerializeField] public RectTransform Body { get; private set; } = null!;
        [field: SerializeField] public RectTransform GhostRow { get; private set; } = null!;

        private readonly List<Button> dots = new ();
        private IObjectPool<Button> dotsPool = null!;
        private Tweener? trackTween;
        private float chromeHeight;
        private float bodyPadding;
        private bool metricsCaptured;
        private bool viewportDirty;
        private float nextCardPeek;

        public int ItemCount { get; private set; }

        public float CardWidth { get; private set; } = 232f;

        public int CardsPerPage { get; private set; } = 1;

        public int PageCount { get; private set; }

        public int CurrentPage { get; private set; }

        public event Action? ViewAllClicked;
        public event Action<int>? PageChanged;

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
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

            ViewAllButton?.onClick.AddListener(() => ViewAllClicked?.Invoke());
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnDisable() =>
            trackTween?.Kill();

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void LateUpdate()
        {
            if (!viewportDirty)
                return;

            viewportDirty = false;
            Recalculate();
            GoToPage(CurrentPage, animate: false);
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnRectTransformDimensionsChange()
        {
            // Runs inside the canvas layout pass, where toggling the dots or dirtying the track layout is not allowed: applied in LateUpdate
            if (isActiveAndEnabled && ItemCount > 0)
                viewportDirty = true;
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

        /// <summary>
        ///     Sizes the row for its cards: the cards per page plus the visible fraction of the next card always fill the viewport,
        ///     so the spacing stretches to fit.
        /// </summary>
        public void Configure(float cardWidth, float cardHeight, int cardsPerPage, float nextCardPeek)
        {
            CaptureMetrics();
            CardWidth = cardWidth;
            CardsPerPage = Mathf.Max(1, cardsPerPage);
            this.nextCardPeek = Mathf.Clamp01(nextCardPeek);

            float bodyHeight = cardHeight + bodyPadding;
            Body.sizeDelta = new Vector2(Body.sizeDelta.x, bodyHeight);
            RootLayout.preferredHeight = bodyHeight + chromeHeight;

            for (var i = 0; i < GhostRow.childCount; i++)
            {
                var ghost = (RectTransform)GhostRow.GetChild(i);
                ghost.sizeDelta = new Vector2(cardWidth, cardHeight);
                ghost.gameObject.SetActive(i < CardsPerPage);
            }

            if (GhostRow.TryGetComponent(out HorizontalLayoutGroup ghostLayout))
                ghostLayout.spacing = Spacing();

            Recalculate();
            GoToPage(CurrentPage, animate: false);
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

        // The prefab is laid out for one card size; the row keeps the same head, dots and hover margins around any other
        private void CaptureMetrics()
        {
            if (metricsCaptured)
                return;

            metricsCaptured = true;
            float ghostHeight = GhostRow.childCount > 0 ? ((RectTransform)GhostRow.GetChild(0)).sizeDelta.y : Body.sizeDelta.y;

            // The root size is driven by the parent layout group and is serialized as 0, so the height comes from the LayoutElement
            chromeHeight = RootLayout.preferredHeight - Body.sizeDelta.y;
            bodyPadding = Body.sizeDelta.y - ghostHeight;
        }

        private void Recalculate()
        {
            TrackLayout.spacing = Spacing();
            PageCount = ItemCount == 0 ? 0 : Mathf.CeilToInt(ItemCount / (float)CardsPerPage);
            RebuildDots();
        }

        private float Spacing()
        {
            // A peeking card runs up to the viewport edge, so it adds one gap and takes the place of the right padding
            bool peeking = nextCardPeek > 0f;
            int gaps = peeking ? CardsPerPage : CardsPerPage - 1;

            if (gaps == 0)
                return MIN_SPACING;

            float rowWidth = Viewport.rect.width - TrackLayout.padding.left - (peeking ? 0 : TrackLayout.padding.right);
            return Mathf.Max(MIN_SPACING, (rowWidth - ((CardsPerPage + nextCardPeek) * CardWidth)) / gaps);
        }

        private void RebuildDots()
        {
            foreach (Button dot in dots)
                dotsPool.Release(dot);

            dots.Clear();
            int dotCount = Mathf.Min(PageCount, MAX_DOTS);

            for (var i = 0; i < dotCount; i++)
            {
                dots.Add(dotsPool.Get());
            }
        }

        private void UpdateControls()
        {
            DotsContainer.gameObject.SetActive(PageCount > 1);

            int selectedDot = DotForPage(CurrentPage);
            bool hasSelectedDot = selectedDot < dots.Count;

            // The selected page shows its own object in the slot of the dot it replaces
            for (var i = 0; i < dots.Count; i++)
                dots[i].gameObject.SetActive(i != selectedDot);

            SelectedDot.gameObject.SetActive(hasSelectedDot);

            if (hasSelectedDot)
                SelectedDot.SetSiblingIndex(dots[selectedDot].transform.GetSiblingIndex());
        }

        private int DotForPage(int page) =>
            PageCount <= MAX_DOTS || PageCount <= 1 ? page : Mathf.RoundToInt(page * (MAX_DOTS - 1) / (float)(PageCount - 1));

        private int PageForDot(int dotIndex) =>
            PageCount <= MAX_DOTS ? dotIndex : Mathf.RoundToInt(dotIndex * (PageCount - 1) / (float)(MAX_DOTS - 1));

        private Button InstantiateDot()
        {
            Button dot = Instantiate(DotPrefab, DotsContainer);
            dot.onClick.AddListener(() => OnDotClicked(dot));
            return dot;
        }

        private void OnDotClicked(Button dot)
        {
            int dotIndex = dots.IndexOf(dot);

            if (dotIndex >= 0)
                GoToPage(PageForDot(dotIndex));
        }
    }
}
