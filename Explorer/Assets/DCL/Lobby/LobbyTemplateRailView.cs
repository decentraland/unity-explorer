using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     Paged rail whose cards are cloned from an inactive template in the prefab.
    ///     What the cards display is up to the owner that fills them; subclasses only wire what a freshly cloned card reports.
    /// </summary>
    public abstract class LobbyTemplateRailView<TCard> : LobbyPagedRailView where TCard : Component
    {
        [SerializeField] private TCard cardTemplate = null!;

        [Tooltip("Lays the cards out in the content; its spacing is part of the stride the pages snap by")]
        [SerializeField] private HorizontalLayoutGroup layout = null!;

        private readonly List<TCard> cards = new ();

        /// <summary>
        ///     Every card cloned so far, shown or hidden. Cards are only ever appended, so indices stay stable.
        /// </summary>
        public IReadOnlyList<TCard> Cards => cards;

        /// <summary>
        ///     Activates the first <paramref name="count" /> cards (cloning the missing ones), hides the rest and rewinds to the first page.
        /// </summary>
        public void ShowCards(int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (i == cards.Count)
                {
                    TCard card = Instantiate(cardTemplate, scrollRect.content);
                    OnCardCreated(card, cards.Count);
                    cards.Add(card);
                }

                cards[i].gameObject.SetActive(true);
            }

            for (int i = count; i < cards.Count; i++)
                cards[i].gameObject.SetActive(false);

            scrollRect.content.anchoredPosition = Vector2.zero;
            OnCountChanged(count, rewind: true);
        }

        // Clones keep the template's width because the layout does not control child widths
        protected override float CardStride() =>
            ((RectTransform)cardTemplate.transform).rect.width + layout.spacing;

        protected abstract void OnCardCreated(TCard card, int index);
    }
}
