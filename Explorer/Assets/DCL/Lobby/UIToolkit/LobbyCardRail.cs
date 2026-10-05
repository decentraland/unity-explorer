using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     The card strip of a lobby section, filled with clones of a card template. The section is handed over on
    ///     every <see cref="Show" /> and the cards outlive it.
    /// </summary>
    public abstract class LobbyCardRail<TCard> where TCard: VisualElement
    {
        private const string CARDS_NAME = "Cards";

        private readonly VisualTreeAsset cardTemplate;
        private readonly List<TCard> cards = new ();

        private VisualElement? strip;
        private LobbyRailElement? rail;

        /// <summary>
        ///     Every card cloned so far, shown or hidden; cards are only ever appended, so indices stay stable.
        /// </summary>
        public IReadOnlyList<TCard> Cards => cards;

        // How many of Cards are shown, counting from the first
        public int Count { get; private set; }

        protected VisualElement? section { get; private set; }

        protected LobbyCardRail(VisualTreeAsset cardTemplate)
        {
            this.cardTemplate = cardTemplate;
        }

        /// <summary>
        ///     Takes <paramref name="newSection" /> over and moves the cards into its strip; the first
        ///     <see cref="Count" /> stay shown with what they last displayed.
        /// </summary>
        public void Show(VisualElement newSection)
        {
            section = newSection;
            rail = newSection.Q<LobbyRailElement>();
            strip = rail ?? newSection.Q(CARDS_NAME);

            for (var i = 0; i < cards.Count; i++)
            {
                cards[i].SetDisplayed(i < Count);
                strip.Add(cards[i]);
            }

            rail?.SetCardCount(Count);

            // A section of a rebuilt hierarchy starts in the state its template authored
            if (Count > 0)
                section.SetDisplayed(true);
        }

        /// <summary>
        ///     Shows the first <paramref name="count" /> cards, cloning the missing ones, and hides the rest.
        ///     The section hides when there is nothing to list.
        /// </summary>
        public void SetCount(int count, bool rewind = true)
        {
            Count = count;

            for (var i = 0; i < count; i++)
            {
                if (i == cards.Count)
                    cards.Add(CreateCard(i));

                cards[i].SetDisplayed(true);
            }

            for (int i = count; i < cards.Count; i++)
                cards[i].SetDisplayed(false);

            rail?.SetCardCount(count, rewind);
            section!.SetDisplayed(count > 0);
            OnCountChanged(count);
        }

        public void Hide()
        {
            section = null;
            strip = null;
            rail = null;
        }

        protected abstract void OnCardCreated(TCard card, int index);

        protected virtual void OnCountChanged(int count) { }

        // The document imports the card's stylesheet, so the card can leave the template's container
        private TCard CreateCard(int index)
        {
            TCard card = cardTemplate.InstantiateForElement<TCard>();
            card.RemoveFromHierarchy();
            OnCardCreated(card, index);
            strip!.Add(card);
            return card;
        }
    }
}
