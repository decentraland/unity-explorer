using DCL.UIToolkit.Elements;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     The card strip of a lobby section, filled with clones of a card template. The section is handed over on
    ///     every <see cref="Show" /> and the cards outlive it. Without cards a rail hides its section, or, when it shows
    ///     an empty state, swaps the strip for the section's element named Loading until its first count and for the one
    ///     named Empty from then on.
    /// </summary>
    public abstract class LobbyCardRail<TCard> where TCard: VisualElement
    {
        private const string CARDS_NAME = "Cards";
        private const string VIEW_ALL_NAME = "ViewAll";
        private const string EMPTY_NAME = "Empty";
        private const string LOADING_NAME = "Loading";

        /// <summary>The View all of the section was clicked; a section without one never reports it.</summary>
        public Action? ViewAllClicked;

        private readonly VisualTreeAsset cardTemplate;
        private readonly List<TCard> cards = new ();
        private readonly bool showsEmptyState;

        private VisualElement? strip;
        private VisualElement? empty;
        private SkeletonLoadingElement? loading;
        private LobbyRailElement? rail;
        private Button? viewAll;
        private bool loaded;

        /// <summary>
        ///     Every card cloned so far, shown or hidden; cards are only ever appended, so indices stay stable.
        /// </summary>
        public IReadOnlyList<TCard> Cards => cards;

        // How many of Cards are shown, counting from the first
        public int Count { get; private set; }

        /// <summary>True until the first <see cref="SetCount" />: nothing has told yet whether there are cards.</summary>
        public bool IsLoading => !loaded;

        protected VisualElement? section { get; private set; }

        protected LobbyCardRail(VisualTreeAsset cardTemplate, bool showsEmptyState = false)
        {
            this.cardTemplate = cardTemplate;
            this.showsEmptyState = showsEmptyState;
        }

        /// <summary>
        ///     Takes <paramref name="newSection" /> over and moves the cards into its strip; the first
        ///     <see cref="Count" /> stay shown with what they last displayed.
        /// </summary>
        public void Show(VisualElement newSection)
        {
            DetachViewAll();
            section = newSection;
            rail = newSection.Q<LobbyRailElement>();
            strip = rail ?? newSection.Q(CARDS_NAME);
            empty = showsEmptyState ? newSection.Q(EMPTY_NAME) : null;
            loading = showsEmptyState ? newSection.Q<SkeletonLoadingElement>(LOADING_NAME) : null;
            viewAll = newSection.Q<Button>(VIEW_ALL_NAME);

            if (viewAll != null)
                viewAll.clicked += OnViewAllClicked;

            for (var i = 0; i < cards.Count; i++)
            {
                cards[i].SetDisplayed(i < Count);
                strip.Add(cards[i]);
            }

            rail?.SetCardCount(Count);
            ApplyDisplay(Count);
        }

        /// <summary>
        ///     Shows the first <paramref name="count" /> cards, cloning the missing ones, and hides the rest.
        ///     Without cards the section hides, or shows its banner when the rail has an empty state.
        /// </summary>
        public void SetCount(int count, bool rewind = true)
        {
            Count = count;
            loaded = true;

            for (var i = 0; i < count; i++)
            {
                if (i == cards.Count)
                    cards.Add(CreateCard(i));

                cards[i].SetDisplayed(true);
            }

            for (int i = count; i < cards.Count; i++)
                cards[i].SetDisplayed(false);

            rail?.SetCardCount(count, rewind);
            ApplyDisplay(count);
            OnCountChanged(count);
        }

        public void Hide()
        {
            DetachViewAll();
            StopLoading();
            section = null;
            strip = null;
            empty = null;
            rail = null;
        }

        protected abstract void OnCardCreated(TCard card, int index);

        protected virtual void OnCountChanged(int count) { }

        private void OnViewAllClicked() =>
            ViewAllClicked?.Invoke();

        // A section of a rebuilt hierarchy starts as its template authored it, so every show settles the display again
        private void ApplyDisplay(int count)
        {
            bool hasCards = count > 0;

            if (!showsEmptyState)
            {
                section!.SetDisplayed(hasCards);
                return;
            }

            bool showsSkeleton = !hasCards && !loaded && loading != null;

            section!.SetDisplayed(true);
            strip!.SetDisplayed(hasCards);
            empty?.SetDisplayed(!hasCards && !showsSkeleton);

            if (loading == null) return;

            loading.SetDisplayed(showsSkeleton);
            loading.IsAnimating = showsSkeleton;
        }

        // A handed-back section outlives the rail, so its skeleton must stop ticking
        private void StopLoading()
        {
            if (loading == null) return;

            loading.IsAnimating = false;
            loading = null;
        }

        // The hierarchy outlives the rail's hold of it, so a handed-back section must stop reporting
        private void DetachViewAll()
        {
            if (viewAll == null) return;

            viewAll.clicked -= OnViewAllClicked;
            viewAll = null;
        }

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
