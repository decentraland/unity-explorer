using DCL.UI.Utilities;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     Horizontal strip that pages by dragging: releasing snaps to the nearest page and one dot per page tracks the position.
    ///     Subclasses own the cards and report how many are shown.
    /// </summary>
    public abstract class LobbyPagedRailView : MonoBehaviour, IEndDragHandler
    {
        private const float SNAP_DURATION = 0.25f;

        [SerializeField] protected ScrollRect scrollRect = null!;

        [Tooltip("Cards laid out per page; the viewport width must fit exactly this many cards plus spacing for the snapping to line up")]
        [SerializeField] protected int cardsPerPage = 3;

        [SerializeField] private LobbyCarouselDotsView dots = null!;

        private Tweener? snapTween;
        private bool scrollListened;
        private int shownCount;

        public int CurrentPage { get; private set; }

        private void Awake()
        {
            scrollRect.SetScrollSensitivityBasedOnPlatform();
        }

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
            dots.Show(PageCount(count));

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
            dots.Select(page);
        }
    }
}
