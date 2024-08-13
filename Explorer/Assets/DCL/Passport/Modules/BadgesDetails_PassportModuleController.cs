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
        private const string ALL_FILTER = "All";

        private readonly BadgesDetails_PassportModuleView view;

        private readonly List<string> badgeTypes = new() { "Explorer", "Socializer", "Collector", "Creator", "Builder" };
        private readonly IObjectPool<ButtonWithSelectableStateView> badgesFilterButtonsPool;
        private readonly List<ButtonWithSelectableStateView> instantiatedBadgesFilterButtons = new ();
        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> badgeDetailCardsPool;
        private readonly List<BadgeDetailCard_PassportFieldView> instantiatedBadgeDetailCards = new ();

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
                },
                actionOnRelease: badgeDetailCardView => badgeDetailCardView.gameObject.SetActive(false));
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

            foreach (string badgeType in badgeTypes)
                CreateFilterButton(badgeType);
        }

        private void CreateFilterButton(string badgeType)
        {
            var allBadgesFilterButton = badgesFilterButtonsPool.Get();
            allBadgesFilterButton.SetSelected(badgeType == ALL_FILTER);
            allBadgesFilterButton.Text.text = badgeType;
            allBadgesFilterButton.Button.onClick.AddListener(() => OnBadgesFilterButtonClicked(badgeType));
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

        private void LoadBadgeDetailCards(string badgeType = "All")
        {
            ClearBadgeDetailCards();

            // TODO (Santi): Request badges for the currentProfile
            int randomBadgesCount = Random.Range(0, BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY + 1);
            for (var i = 0; i < randomBadgesCount; i++)
            {
                var badgeDetailCard = badgeDetailCardsPool.Get();
                badgeDetailCard.BadgeNameText.text = $"Badge {badgeType} {i + 1}";
                badgeDetailCard.BadgeImage.sprite = null;
                instantiatedBadgeDetailCards.Add(badgeDetailCard);
            }
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
            foreach (BadgeDetailCard_PassportFieldView badgeOverviewItem in instantiatedBadgeDetailCards)
                badgeDetailCardsPool.Release(badgeOverviewItem);

            instantiatedBadgeDetailCards.Clear();
        }
    }
}
