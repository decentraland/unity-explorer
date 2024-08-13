using DCL.Passport.Fields;
using DCL.Profiles;
using DCL.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Passport.Modules
{
    public class BadgesDetails_PassportModuleController : IPassportModuleController
    {
        private const int BADGES_FILTER_BUTTONS_POOL_DEFAULT_CAPACITY = 6;
        private const int BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY = 50;
        private const int GRID_ITEMS_PER_ROW = 6;
        private const string ALL_FILTER = "All";

        private readonly BadgesDetails_PassportModuleView view;

        private readonly List<string> badgeCategories = new() { "Explorer", "Socializer", "Collector", "Creator", "Builder" };
        private readonly IObjectPool<ButtonWithSelectableStateView> badgesFilterButtonsPool;
        private readonly List<ButtonWithSelectableStateView> instantiatedBadgesFilterButtons = new ();
        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> badgeDetailCardsPool;
        private readonly List<BadgeDetailCard_PassportFieldView> instantiatedBadgeDetailCards = new ();
        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> emptyItemsPool;
        private readonly List<BadgeDetailCard_PassportFieldView> instantiatedEmptyItems = new ();

        private Profile? currentProfile;
        private string? currentFilter;

        public BadgesDetails_PassportModuleController(BadgesDetails_PassportModuleView view)
        {
            this.view = view;

            badgesFilterButtonsPool = new ObjectPool<ButtonWithSelectableStateView>(
                InstantiateBadgesFilterButtonPrefab,
                defaultCapacity: BADGES_FILTER_BUTTONS_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgesFilterButton =>
                {
                    badgesFilterButton.gameObject.SetActive(true);
                    badgesFilterButton.gameObject.transform.SetAsLastSibling();
                },
                actionOnRelease: badgesFilterButton => badgesFilterButton.gameObject.SetActive(false));

            badgeDetailCardsPool = new ObjectPool<BadgeDetailCard_PassportFieldView>(
                InstantiateBadgeDetailCardPrefab,
                defaultCapacity: BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgeDetailCardView =>
                {
                    badgeDetailCardView.gameObject.SetActive(true);
                    badgeDetailCardView.gameObject.transform.SetAsFirstSibling();
                    badgeDetailCardView.SetAsSelected(false);
                },
                actionOnRelease: badgeDetailCardView => badgeDetailCardView.gameObject.SetActive(false));

            emptyItemsPool = new ObjectPool<BadgeDetailCard_PassportFieldView>(
                InstantiateBadgeDetailCardPrefab,
                defaultCapacity: GRID_ITEMS_PER_ROW - 1,
                actionOnGet: emptyItemView =>
                {
                    emptyItemView.gameObject.SetActive(true);
                    emptyItemView.SetInvisible(true);
                    emptyItemView.gameObject.transform.SetAsFirstSibling();
                },
                actionOnRelease: emptyItemView => emptyItemView.gameObject.SetActive(false));
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            LoadBadgesFilterButtons();
            LoadBadgeDetailCards();
        }

        public void Clear()
        {
            ClearBadgesFilterButtons();
            ClearBadgeDetailCards();
        }

        public void Dispose() =>
            Clear();

        private ButtonWithSelectableStateView InstantiateBadgesFilterButtonPrefab()
        {
            ButtonWithSelectableStateView badgesFilterButton = Object.Instantiate(view.BadgesFilterButtonPrefab, view.BadgesFilterButtonsContainer);
            return badgesFilterButton;
        }

        private BadgeDetailCard_PassportFieldView InstantiateBadgeDetailCardPrefab()
        {
            BadgeDetailCard_PassportFieldView badgeDetailCareView = Object.Instantiate(view.BadgeDetailCardPrefab, view.BadgeDetailCardsContainer);
            return badgeDetailCareView;
        }

        private void LoadBadgesFilterButtons()
        {
            ClearBadgesFilterButtons();
            CreateFilterButton(ALL_FILTER);
            currentFilter = ALL_FILTER;

            foreach (string category in badgeCategories)
                CreateFilterButton(category);
        }

        private void CreateFilterButton(string badgeCategory)
        {
            var allBadgesFilterButton = badgesFilterButtonsPool.Get();
            allBadgesFilterButton.SetSelected(badgeCategory == ALL_FILTER);
            allBadgesFilterButton.Text.text = badgeCategory;
            allBadgesFilterButton.Button.onClick.AddListener(() => OnBadgesFilterButtonClicked(badgeCategory));
            instantiatedBadgesFilterButtons.Add(allBadgesFilterButton);
        }

        private void OnBadgesFilterButtonClicked(string filter)
        {
            if (currentFilter == filter)
                return;

            foreach (ButtonWithSelectableStateView filterButton in instantiatedBadgesFilterButtons)
                filterButton.SetSelected(filterButton.Text.text == filter);

            LoadBadgeDetailCards(filter);
            currentFilter = filter;
        }

        private void LoadBadgeDetailCards(string badgeCategory = "All")
        {
            ClearBadgeDetailCards();

            // TODO (Santi): Request badges for the currentProfile
            int randomBadgesCount = Random.Range(0, BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY + 1);
            for (var i = 0; i < randomBadgesCount; i++)
            {
                var badgeDetailCard = badgeDetailCardsPool.Get();
                badgeDetailCard.Setup(
                    $"Badge {badgeCategory} {i + 1}",
                    Random.Range(0, 2) == 0,
                    badgeCategory,
                    null,
                    "Feb. 2024",
                    Random.Range(0, 2) == 0,
                    Random.Range(0, 2) == 0,
                    Random.Range(0, 101));
                badgeDetailCard.SetAsSelected(i == 0);

                badgeDetailCard.Button.onClick.AddListener(() =>
                {
                    foreach (BadgeDetailCard_PassportFieldView badge in instantiatedBadgeDetailCards)
                        badge.SetAsSelected(false);

                    badgeDetailCard.SetAsSelected(true);
                });

                instantiatedBadgeDetailCards.Add(badgeDetailCard);
            }

            int missingEmptyItems = CalculateMissingEmptyItems(instantiatedBadgeDetailCards.Count);
            for (var i = 0; i < missingEmptyItems; i++)
            {
                var emptyItem = emptyItemsPool.Get();
                emptyItem.gameObject.name = "EmptyItem";
                instantiatedEmptyItems.Add(emptyItem);
            }
        }

        private static int CalculateMissingEmptyItems(int totalItems)
        {
            int remainder = totalItems % GRID_ITEMS_PER_ROW;
            int missingItems = remainder == 0 ? 0 : GRID_ITEMS_PER_ROW - remainder;
            return missingItems;
        }

        private void ClearBadgesFilterButtons()
        {
            foreach (ButtonWithSelectableStateView badgesFilterButton in instantiatedBadgesFilterButtons)
            {
                badgesFilterButton.Button.onClick.RemoveAllListeners();
                badgesFilterButtonsPool.Release(badgesFilterButton);
            }

            instantiatedBadgesFilterButtons.Clear();
            currentFilter = null;
        }

        private void ClearBadgeDetailCards()
        {
            ClearEmptyItems();

            foreach (BadgeDetailCard_PassportFieldView badgeDetailCard in instantiatedBadgeDetailCards)
            {
                badgeDetailCard.Button.onClick.RemoveAllListeners();
                badgeDetailCardsPool.Release(badgeDetailCard);
            }

            instantiatedBadgeDetailCards.Clear();
        }

        private void ClearEmptyItems()
        {
            foreach (BadgeDetailCard_PassportFieldView emptyItem in instantiatedEmptyItems)
                emptyItemsPool.Release(emptyItem);

            instantiatedEmptyItems.Clear();
        }
    }
}
