using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     The card strip of a lobby section, filled with clones
    ///     of a card template. The strip is the section's <see cref="LobbyRailElement" /> when it pages, or a plain row named
    ///     <see cref="CARDS_NAME" /> when the section shows a fixed few. The section only exists while the document is shown, so it is
    ///     handed over on every <see cref="Show" />; the cards outlive it and move into the next one. What the cards display is up to
    ///     the owner that fills them; subclasses only wire what a freshly cloned card reports.
    /// </summary>
    public abstract class LobbyCardRail<TCard> where TCard: VisualElement
    {
        private const string CARDS_NAME = "Cards";

        private readonly VisualTreeAsset cardTemplate;
        private readonly List<TCard> cards = new ();

        private VisualElement? strip;
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
        ///     Takes <paramref name="newSection" /> over and moves the cards into its strip. The first <see cref="Count" /> stay shown with
        ///     what they last displayed, so the row does not go empty until <see cref="SetCount" /> refreshes it.
        /// </summary>
        public void Show(VisualElement newSection)
        {
            Section = newSection;
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
                Section.SetDisplayed(true);
        }

        /// <summary>
        ///     Shows the first <paramref name="count" /> cards (cloning the missing ones), hides the rest and the section when there is
        ///     nothing to list. A rail rewinds to its first page, or stays on its page or the last one the cards still fill; a plain row
        ///     has no pages to keep.
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
            Section!.SetDisplayed(count > 0);
            OnCountChanged(count);
        }

        // The section keeps the state the last SetCount left it in, so the row comes back as it was on the next Show
        public void Hide()
        {
            Section = null;
            strip = null;
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
            strip!.Add(card);
            return card;
        }
    }
}
