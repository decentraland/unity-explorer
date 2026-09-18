using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     Horizontal strip that pages by dragging: releasing snaps to the nearest page and one dot per page tracks the position.
    ///     Dots are cloned from an inactive template in the prefab. Subclasses own the cards and report how many are shown.
    /// </summary>
    public abstract class LobbyPagedRailView : MonoBehaviour, IEndDragHandler
    {
        private const float SNAP_DURATION = 0.25f;
        private const float DOT_ANIMATION_DURATION = 0.2f;

        [SerializeField] protected ScrollRect scrollRect = null!;

        [Tooltip("Cards laid out per page; the viewport width must fit exactly this many cards plus spacing for the snapping to line up")]
        [SerializeField] protected int cardsPerPage = 3;

        [Header("Dots")]
        [SerializeField] private Image dotTemplate = null!;
        [SerializeField] private Color selectedDotColor = Color.white;
        [SerializeField] private Color dotColor = Color.gray;
        [SerializeField] private float selectedDotWidth = 24f;
        [SerializeField] private float dotWidth = 8f;

        private readonly List<Image> dots = new ();

        private Tweener? snapTween;
        private bool scrollListened;
        private int shownCount;

        public int CurrentPage { get; private set; }

        public void OnEndDrag(PointerEventData _) =>
            SnapTo(PageAt(-scrollRect.content.anchoredPosition.x));

        /// <summary>
        ///     Rebuilds the dots for <paramref name="count" /> cards and either rewinds to the first page or keeps the current one,
        ///     clamped so a shrinking list never leaves the view on a page that no longer exists.
        /// </summary>
        protected void OnCountChanged(int count, bool rewind)
        {
            if (!scrollListened)
            {
                scrollRect.onValueChanged.AddListener(OnScrolled);
                scrollListened = true;
            }

            shownCount = count;
            ShowDots(PageCount(count));

            if (rewind)
            {
                snapTween?.Kill();
                scrollRect.velocity = Vector2.zero;
                SelectPage(0);
            }
            else
                SnapTo(CurrentPage);
        }

        protected int PageCount(int cardCount) =>
            (cardCount + cardsPerPage - 1) / Mathf.Max(1, cardsPerPage);

        private float PageWidth() =>
            scrollRect.viewport.rect.width;

        // The content cannot scroll past its end, so the last page starts wherever the content ends rather than a full page in
        private float PageOffset(int page) =>
            Mathf.Min(page * PageWidth(), Mathf.Max(0f, scrollRect.content.rect.width - PageWidth()));

        private int PageAt(float offset)
        {
            var nearest = 0;
            float nearestDistance = float.MaxValue;

            for (var page = 0; page < PageCount(shownCount); page++)
            {
                float distance = Mathf.Abs(offset - PageOffset(page));
                if (distance >= nearestDistance) continue;

                nearestDistance = distance;
                nearest = page;
            }

            return nearest;
        }

        private void ShowDots(int pageCount)
        {
            // A single page needs no navigation hint
            int visibleDots = pageCount > 1 ? pageCount : 0;

            for (var i = 0; i < visibleDots; i++)
            {
                if (i == dots.Count)
                    dots.Add(Instantiate(dotTemplate, dotTemplate.transform.parent));

                dots[i].gameObject.SetActive(true);
            }

            for (int i = visibleDots; i < dots.Count; i++)
                dots[i].gameObject.SetActive(false);
        }

        private void OnScrolled(Vector2 _) =>
            SelectPage(PageAt(-scrollRect.content.anchoredPosition.x));

        private void SnapTo(int page)
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, PageCount(shownCount) - 1));
            RectTransform content = scrollRect.content;
            var target = new Vector2(-PageOffset(page), content.anchoredPosition.y);

            snapTween?.Kill();
            scrollRect.velocity = Vector2.zero;

            snapTween = DOTween.To(() => content.anchoredPosition, position => content.anchoredPosition = position, target, SNAP_DURATION)
                               .SetEase(Ease.OutCubic)
                               .SetLink(gameObject);

            SelectPage(page);
        }

        private void SelectPage(int page)
        {
            CurrentPage = page;

            for (var i = 0; i < dots.Count; i++)
            {
                if (!dots[i].gameObject.activeSelf) continue;

                bool selected = i == page;
                RectTransform dot = dots[i].rectTransform;
                dots[i].color = selected ? selectedDotColor : dotColor;

                DOTween.To(() => dot.sizeDelta, size => dot.sizeDelta = size, new Vector2(selected ? selectedDotWidth : dotWidth, dot.sizeDelta.y), DOT_ANIMATION_DURATION)
                       .SetEase(Ease.OutCubic)
                       .SetLink(dot.gameObject);
            }
        }
    }
}
