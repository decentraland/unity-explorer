using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     Horizontal strip of cards that pages by dragging: releasing snaps to the nearest page
    ///     and one dot per page tracks the position. Cards and dots are cloned from inactive templates in the prefab.
    ///     What the cards display is up to the owner that fills them; the view only reports which card was clicked.
    /// </summary>
    public class LobbyCarouselView : MonoBehaviour, IEndDragHandler
    {
        private const float SNAP_DURATION = 0.25f;
        private const float DOT_ANIMATION_DURATION = 0.2f;

        [SerializeField] private ScrollRect scrollRect = null!;
        [SerializeField] private LobbyCardView cardTemplate = null!;

        [Tooltip("Cards laid out per page; the viewport width must fit exactly this many cards plus spacing for the snapping to line up")]
        [SerializeField] private int cardsPerPage = 3;

        [Header("Dots")]
        [SerializeField] private Image dotTemplate = null!;
        [SerializeField] private Color selectedDotColor = Color.white;
        [SerializeField] private Color dotColor = Color.gray;
        [SerializeField] private float selectedDotWidth = 24f;
        [SerializeField] private float dotWidth = 8f;

        private readonly List<LobbyCardView> cards = new ();
        private readonly List<Image> dots = new ();

        private Tweener? snapTween;
        private bool scrollListened;
        private int shownCount;

        /// <summary>
        ///     Index in <see cref="Cards" /> of the card that was clicked.
        /// </summary>
        public Action<int>? CardClicked;

        public int CurrentPage { get; private set; }

        /// <summary>
        ///     Every card cloned so far, shown or hidden. Cards are only ever appended, so indices stay stable.
        /// </summary>
        public IReadOnlyList<LobbyCardView> Cards => cards;

        /// <summary>
        ///     Activates the first <paramref name="count" /> cards (cloning the missing ones), hides the rest and rewinds to the first page.
        /// </summary>
        public void ShowCards(int count)
        {
            if (!scrollListened)
            {
                scrollRect.onValueChanged.AddListener(OnScrolled);
                scrollListened = true;
            }

            shownCount = count;

            for (var i = 0; i < count; i++)
            {
                if (i == cards.Count)
                    cards.Add(CreateCard());

                cards[i].gameObject.SetActive(true);
            }

            for (int i = count; i < cards.Count; i++)
                cards[i].gameObject.SetActive(false);

            ShowDots(PageCount(count));

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

        private LobbyCardView CreateCard()
        {
            LobbyCardView card = Instantiate(cardTemplate, scrollRect.content);
            int index = cards.Count;
            card.Button.onClick.AddListener(() => CardClicked?.Invoke(index));
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
            SelectPage(Mathf.Clamp(Mathf.RoundToInt(-scrollRect.content.anchoredPosition.x / PageWidth()), 0, Mathf.Max(0, PageCount(shownCount) - 1)));

        private void SnapTo(int page)
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, PageCount(shownCount) - 1));
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
