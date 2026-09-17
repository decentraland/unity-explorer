using DCL.Communities;
using DCL.PlacesAPIService;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     Horizontal strip of place cards that pages by dragging: releasing snaps to the nearest page
    ///     and one dot per page tracks the position. Cards and dots are cloned from inactive templates in the prefab.
    /// </summary>
    public class LobbyPlacesCarousel : MonoBehaviour, IEndDragHandler
    {
        private const float SNAP_DURATION = 0.25f;
        private const float DOT_ANIMATION_DURATION = 0.2f;

        [SerializeField] private ScrollRect scrollRect = null!;
        [SerializeField] private LobbyPlaceCardView cardTemplate = null!;

        [Tooltip("Cards laid out per page; the viewport width must fit exactly this many cards plus spacing for the snapping to line up")]
        [SerializeField] private int cardsPerPage = 3;

        [Header("Dots")]
        [SerializeField] private Image dotTemplate = null!;
        [SerializeField] private Color selectedDotColor = Color.white;
        [SerializeField] private Color dotColor = Color.gray;
        [SerializeField] private float selectedDotWidth = 24f;
        [SerializeField] private float dotWidth = 8f;

        private readonly List<LobbyPlaceCardView> cards = new ();
        private readonly List<Image> dots = new ();

        private Tweener? snapTween;
        private bool scrollListened;
        private int shownCards;

        public Action<PlacesData.PlaceInfo>? PlaceClicked;

        public int CurrentPage { get; private set; }

        public IReadOnlyList<LobbyPlaceCardView> Cards => cards;

        public void Show(IReadOnlyList<PlacesData.PlaceInfo> places, ThumbnailLoader thumbnailLoader, CancellationToken ct)
        {
            if (!scrollListened)
            {
                scrollRect.onValueChanged.AddListener(OnScrolled);
                scrollListened = true;
            }

            shownCards = places.Count;

            for (var i = 0; i < places.Count; i++)
            {
                if (i == cards.Count)
                    cards.Add(CreateCard());

                cards[i].Show(places[i], thumbnailLoader, ct);
            }

            for (int i = places.Count; i < cards.Count; i++)
                cards[i].Hide();

            ShowDots(PageCount(places.Count));

            snapTween?.Kill();
            scrollRect.velocity = Vector2.zero;
            scrollRect.content.anchoredPosition = Vector2.zero;
            SelectPage(0);
        }

        public void OnEndDrag(PointerEventData _) =>
            SnapTo(Mathf.RoundToInt(-scrollRect.content.anchoredPosition.x / PageWidth()));

        private int PageCount(int cardCount) =>
            (cardCount + cardsPerPage - 1) / Mathf.Max(1, cardsPerPage);

        private float PageWidth() =>
            scrollRect.viewport.rect.width;

        private LobbyPlaceCardView CreateCard()
        {
            LobbyPlaceCardView card = Instantiate(cardTemplate, scrollRect.content);
            card.Button.onClick.AddListener(() => { if (card.Place is { } place) PlaceClicked?.Invoke(place); });
            return card;
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
            SelectPage(Mathf.Clamp(Mathf.RoundToInt(-scrollRect.content.anchoredPosition.x / PageWidth()), 0, Mathf.Max(0, PageCount(shownCards) - 1)));

        private void SnapTo(int page)
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, PageCount(shownCards) - 1));
            RectTransform content = scrollRect.content;
            float maxOffset = Mathf.Max(0f, content.rect.width - PageWidth());
            var target = new Vector2(-Mathf.Min(page * PageWidth(), maxOffset), content.anchoredPosition.y);

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
