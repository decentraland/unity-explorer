using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Paged rail whose cards are cloned from an inactive template in the prefab.
    ///     What the cards display is up to the owner that fills them; the view only reports which card was clicked.
    /// </summary>
    public class LobbyCarouselView : LobbyPagedRailView
    {
        [SerializeField] private LobbyCardView cardTemplate = null!;

        private readonly List<LobbyCardView> cards = new ();

        /// <summary>
        ///     Index in <see cref="Cards" /> of the card that was clicked.
        /// </summary>
        public Action<int>? CardClicked;

        /// <summary>
        ///     Index in <see cref="Cards" /> of the card whose Jump in button was clicked.
        /// </summary>
        public Action<int>? CardJumpInClicked;

        /// <summary>
        ///     Every card cloned so far, shown or hidden. Cards are only ever appended, so indices stay stable.
        /// </summary>
        public IReadOnlyList<LobbyCardView> Cards => cards;

        /// <summary>
        ///     Activates the first <paramref name="count" /> cards (cloning the missing ones), hides the rest and rewinds to the first page.
        /// </summary>
        public void ShowCards(int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (i == cards.Count)
                    cards.Add(CreateCard());

                cards[i].gameObject.SetActive(true);
            }

            for (int i = count; i < cards.Count; i++)
                cards[i].gameObject.SetActive(false);

            scrollRect.content.anchoredPosition = Vector2.zero;
            OnCountChanged(count, rewind: true);
        }

        private LobbyCardView CreateCard()
        {
            LobbyCardView card = Instantiate(cardTemplate, scrollRect.content);
            int index = cards.Count;
            card.Button.onClick.AddListener(() => CardClicked?.Invoke(index));

            if (card.JumpInButton != null)
                card.JumpInButton.Button.onClick.AddListener(() => CardJumpInClicked?.Invoke(index));

            return card;
        }
    }
}
