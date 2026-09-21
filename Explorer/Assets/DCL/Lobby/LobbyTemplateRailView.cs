using System.Collections.Generic;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Paged rail whose cards are cloned from an inactive template in the prefab.
    ///     What the cards display is up to the owner that fills them; subclasses only wire what a freshly cloned card reports.
    /// </summary>
    public abstract class LobbyTemplateRailView<TCard> : LobbyPagedRailView where TCard : Component
    {
        [SerializeField] private TCard cardTemplate = null!;

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

        protected abstract void OnCardCreated(TCard card, int index);
    }
}
