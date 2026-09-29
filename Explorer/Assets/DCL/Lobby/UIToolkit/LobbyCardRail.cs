using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="LobbyTemplateRailView{TCard}" />: the <see cref="LobbyRailElement" /> of a lobby section,
    ///     filled with clones of a card template. The section only exists while the document is shown, so it is handed over on every
    ///     <see cref="Show" />; the cards outlive it and move into the next one. What the cards display is up to the owner that fills
    ///     them; subclasses only wire what a freshly cloned card reports.
    /// </summary>
    public abstract class LobbyCardRail<TCard> where TCard: VisualElement
    {
        private readonly VisualTreeAsset cardTemplate;
        private readonly List<TCard> cards = new ();

        private LobbyRailElement? rail;

        /// <summary>
        ///     Every card cloned so far, shown or hidden. Cards are only ever appended, so indices stay stable.
        /// </summary>
        public IReadOnlyList<TCard> Cards => cards;

        /// <summary>
        ///     How many of <see cref="Cards" /> are shown, counting from the first.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        ///     The section handed over by the last <see cref="Show" />; null between <see cref="Hide" /> and the next show.
        /// </summary>
        protected VisualElement? Section { get; private set; }

        protected LobbyCardRail(VisualTreeAsset cardTemplate)
        {
            this.cardTemplate = cardTemplate;
        }

        /// <summary>
        ///     Takes <paramref name="newSection" /> over and moves the cards into its rail, hidden, until <see cref="SetCount" /> shows them.
        /// </summary>
        public void Show(VisualElement newSection)
        {
            Section = newSection;
            rail = newSection.Q<LobbyRailElement>();
            Count = 0;

            foreach (TCard card in cards)
            {
                card.SetDisplayed(false);
                rail.Add(card);
            }

            rail.SetCardCount(0);
        }

        /// <summary>
        ///     Shows the first <paramref name="count" /> cards (cloning the missing ones), hides the rest and the section when there is
        ///     nothing to list. Rewinding jumps to the first page; otherwise the rail stays on its page, or on the last one the cards still fill.
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

            rail!.SetCardCount(count, rewind);
            Section!.SetDisplayed(count > 0);
            OnCountChanged(count);
        }

        public void Hide()
        {
            Section?.SetDisplayed(false);
            Section = null;
            rail = null;
        }

        protected abstract void OnCardCreated(TCard card, int index);

        protected virtual void OnCountChanged(int count) { }

        // The card's stylesheet is imported by the document, so the card can leave the container the template was cloned into
        private TCard CreateCard(int index)
        {
            TCard card = cardTemplate.InstantiateForElement<TCard>();
            card.RemoveFromHierarchy();
            OnCardCreated(card, index);
            rail!.Add(card);
            return card;
        }
    }
}
