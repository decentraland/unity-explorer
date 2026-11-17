using DCL.UI;
using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;

// Optional serialized references: Unity writes the backing field, so the private setter has no caller
// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace DCL.Shop
{
    /// <summary>
    ///     A horizontal strip of cards that pages by the arrows or the dots and scrolls freely by dragging; a released drag
    ///     settles on the nearest card. The arrows wrap round past either end, a drag stops there. The pager shows its pill
    ///     and its arrows while the pointer is over the row.
    /// </summary>
    public class ShopCarouselView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const int MAX_DOTS = 6;
        private const int DOTS_POOL_CAPACITY = 6;
        private const float SNAP_DURATION = 0.25f;
        private const float HOVER_FADE_DURATION = 0.15f;
        private const float DOT_DURATION = 0.2f;
        private const float SELECTED_DOT_WIDTH = 40f;
        private const float MIN_SPACING = 8f;

        // Seconds of its release speed a flung drag runs on for, capped at a page
        private const float FLING_PROJECTION = 0.12f;

        // A pointer that rested longer than this before its release was not flung
        private const float FLING_STALE_SECONDS = 0.1f;

        // Slack under which the strip counts as having reached its end
        private const float END_TOLERANCE = 0.5f;

        [field: SerializeField] public TMP_Text Title { get; private set; } = null!;
        [field: SerializeField] public Button? ViewAllButton { get; private set; }
        [field: SerializeField] public RectTransform Viewport { get; private set; } = null!;
        [field: SerializeField] public RectTransform Track { get; private set; } = null!;
        [field: SerializeField] public HorizontalLayoutGroup TrackLayout { get; private set; } = null!;
        [field: SerializeField] public Transform DotsContainer { get; private set; } = null!;
        [field: SerializeField] public Button DotPrefab { get; private set; } = null!;
        [field: SerializeField] public Image PagerBackground { get; private set; } = null!;
        [field: SerializeField] public Button PreviousButton { get; private set; } = null!;
        [field: SerializeField] public Button NextButton { get; private set; } = null!;
        [field: SerializeField] public CanvasGroup PreviousGroup { get; private set; } = null!;
        [field: SerializeField] public CanvasGroup NextGroup { get; private set; } = null!;
        [field: SerializeField] public SkeletonLoadingView? Skeleton { get; private set; }
        [field: SerializeField] public GameObject ControlsRoot { get; private set; } = null!;
        [field: SerializeField] public LayoutElement RootLayout { get; private set; } = null!;
        [field: SerializeField] public RectTransform Body { get; private set; } = null!;
        [field: SerializeField] public RectTransform GhostRow { get; private set; } = null!;

        private readonly List<Button> dots = new ();
        private IObjectPool<Button> dotsPool = null!;
        private Tweener? trackTween;
        private Tweener? hoverTween;
        private ScrollRect? parentScroll;
        private Vector2 dotSize;
        private float pagerAlpha;
        private float hoverAmount;
        private bool hovered;
        private bool dragging;
        private bool forwardingDrag;
        private float dragStartX;
        private float dragStartOffset;
        private float lastDragX;
        private float lastDragTime;

        // Viewport units per second, from the last two moves of the drag
        private float dragVelocity;

        // The card at the left edge of the viewport since the last snap
        private int currentCard;
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

        private float offset => -Track.anchoredPosition.x;

        private float cardStride => CardWidth + TrackLayout.spacing;

        public event Action? ViewAllClicked;

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
            PreviousButton.onClick.AddListener(OnPreviousClicked);
            NextButton.onClick.AddListener(OnNextClicked);

            parentScroll = transform.parent != null ? transform.parent.GetComponentInParent<ScrollRect>() : null;
            dotSize = ((RectTransform)DotPrefab.transform).sizeDelta;
            pagerAlpha = PagerBackground.color.a;
            ApplyHover(0f);
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnDisable()
        {
            trackTween?.Kill();
            hoverTween?.Kill();
            hovered = false;
            dragging = false;
            forwardingDrag = false;
            ApplyHover(0f);
            ApplyOffset(CardOffset(currentCard));
            SelectPage(CurrentPage, animate: false);
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void LateUpdate()
        {
            if (!viewportDirty)
                return;

            viewportDirty = false;
            Recalculate();
            Replace();
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
            Replace();
        }

        public void SetItemCount(int count)
        {
            ItemCount = count;
            Recalculate();
            SnapToCard(0, 0, animate: false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            RefreshHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            RefreshHover();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (parentScroll != null)
                parentScroll.OnInitializePotentialDrag(eventData);
        }

        // A mostly vertical drag belongs to the scroll view the row sits in
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            Vector2 travel = eventData.position - eventData.pressPosition;

            if (PageCount == 0 || Mathf.Abs(travel.y) > Mathf.Abs(travel.x))
            {
                if (parentScroll == null)
                    return;

                forwardingDrag = true;
                parentScroll.OnBeginDrag(eventData);
                return;
            }

            if (!TryGetViewportX(eventData, out float x))
                return;

            // Based on where the strip is drawn, mid-snap included, so it follows from there rather than jumping
            trackTween?.Kill();
            dragging = true;
            dragStartX = x;
            dragStartOffset = offset;
            dragVelocity = 0f;
            lastDragX = x;
            lastDragTime = UnityEngine.Time.unscaledTime;
            RefreshHover();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (forwardingDrag && parentScroll != null)
            {
                parentScroll.OnDrag(eventData);
                return;
            }

            if (!dragging || !TryGetViewportX(eventData, out float x))
                return;

            float elapsed = UnityEngine.Time.unscaledTime - lastDragTime;

            if (elapsed > 0f)
            {
                dragVelocity = (x - lastDragX) / elapsed;
                lastDragX = x;
                lastDragTime = UnityEngine.Time.unscaledTime;
            }

            ApplyOffset(Mathf.Clamp(dragStartOffset - (x - dragStartX), 0f, MaxOffset()));
        }

        // The strip settles on the nearest card the fling carries it to
        public void OnEndDrag(PointerEventData eventData)
        {
            if (forwardingDrag)
            {
                forwardingDrag = false;

                if (parentScroll != null)
                    parentScroll.OnEndDrag(eventData);

                return;
            }

            if (!dragging)
                return;

            dragging = false;
            RefreshHover();

            float velocity = UnityEngine.Time.unscaledTime - lastDragTime > FLING_STALE_SECONDS ? 0f : dragVelocity;
            float pageWidth = CardsPerPage * cardStride;
            float fling = Mathf.Clamp(-velocity * FLING_PROJECTION, -pageWidth, pageWidth);
            int card = Nearest(offset + fling, ItemCount, 1);
            SnapToCard(card, Nearest(CardOffset(card), PageCount, CardsPerPage), animate: true);
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

        // The current card is put back in place after the strides changed
        private void Replace()
        {
            if (dragging)
                return;

            currentCard = Mathf.Clamp(currentCard, 0, Mathf.Max(0, ItemCount - 1));
            SnapToCard(currentCard, Nearest(CardOffset(currentCard), PageCount, CardsPerPage), animate: false);
        }

        // From the start the row wraps round to the last page
        private void OnPreviousClicked() =>
            SnapToPage(currentCard == 0 ? PageCount - 1 : Mathf.Max(0, currentCard - 1) / CardsPerPage);

        // Past the last page the row wraps round to the first, once the strip has reached its end
        private void OnNextClicked()
        {
            int page = (currentCard / CardsPerPage) + 1;
            SnapToPage(page < PageCount || offset < MaxOffset() - END_TOLERANCE ? page : 0);
        }

        private void SnapToPage(int page)
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, PageCount - 1));
            SnapToCard(page * CardsPerPage, page, animate: true);
        }

        private void SnapToCard(int card, int page, bool animate)
        {
            currentCard = card;
            float target = CardOffset(card);

            trackTween?.Kill();

            if (animate)
                trackTween = Track.DOAnchorPosX(-target, SNAP_DURATION).SetEase(Ease.OutQuad);
            else
                ApplyOffset(target);

            SelectPage(page, animate);
        }

        private void ApplyOffset(float value) =>
            Track.anchoredPosition = new Vector2(-value, Track.anchoredPosition.y);

        // The strip ends at its last card, so the last cards share the offset of the end
        private float CardOffset(int card) =>
            Mathf.Min(card * cardStride, MaxOffset());

        private float MaxOffset()
        {
            if (ItemCount == 0)
                return 0f;

            float contentWidth = TrackLayout.padding.horizontal + (ItemCount * CardWidth) + ((ItemCount - 1) * TrackLayout.spacing);
            return Mathf.Max(0f, contentWidth - Viewport.rect.width);
        }

        // The step, out of count steps of cardsPerStep cards, whose offset is the closest to the given one
        private int Nearest(float at, int count, int cardsPerStep)
        {
            var nearest = 0;
            float nearestDistance = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                float distance = Mathf.Abs(at - CardOffset(i * cardsPerStep));

                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = i;
            }

            return nearest;
        }

        private bool TryGetViewportX(PointerEventData eventData, out float x)
        {
            bool hit = RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, eventData.position, eventData.pressEventCamera, out Vector2 local);
            x = local.x;
            return hit;
        }

        private void RefreshHover()
        {
            hoverTween?.Kill();
            hoverTween = DOTween.To(() => hoverAmount, ApplyHover, hovered || dragging ? 1f : 0f, HOVER_FADE_DURATION).SetEase(Ease.OutQuad);
        }

        private void ApplyHover(float amount)
        {
            hoverAmount = amount;
            Color pagerColor = PagerBackground.color;
            pagerColor.a = pagerAlpha * amount;
            PagerBackground.color = pagerColor;
            PreviousGroup.alpha = amount;
            NextGroup.alpha = amount;
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
            int dotCount = PageCount > 1 ? Mathf.Min(PageCount, MAX_DOTS) : 0;

            for (var i = 0; i < dotCount; i++)
            {
                dots.Add(dotsPool.Get());
            }

            // The pill goes with the dots: nothing to page through leaves no arrows either
            PagerBackground.gameObject.SetActive(dotCount > 0);
        }

        // The dot of the page stretches into the selected look; disabled, its button shows the selected colour
        private void SelectPage(int page, bool animate)
        {
            CurrentPage = page;
            int selectedDot = DotForPage(page);

            for (var i = 0; i < dots.Count; i++)
            {
                bool selected = i == selectedDot;
                var dotTransform = (RectTransform)dots[i].transform;
                var size = new Vector2(selected ? SELECTED_DOT_WIDTH : dotSize.x, dotSize.y);

                dots[i].interactable = !selected;
                dotTransform.DOKill();

                if (animate && !Mathf.Approximately(dotTransform.sizeDelta.x, size.x))
                    dotTransform.DOSizeDelta(size, DOT_DURATION).SetEase(Ease.OutQuad);
                else
                    dotTransform.sizeDelta = size;
            }
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
                SnapToPage(PageForDot(dotIndex));
        }
    }
}
