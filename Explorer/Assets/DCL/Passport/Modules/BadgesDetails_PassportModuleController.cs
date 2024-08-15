using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.Passport.Fields;
using DCL.Profiles;
using DCL.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules
{
    public class BadgesDetails_PassportModuleController : IPassportModuleController
    {
        private const int BADGES_FILTER_BUTTONS_POOL_DEFAULT_CAPACITY = 6;
        private const int BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY = 50;
        private const int GRID_ITEMS_PER_ROW = 6;
        private const string ALL_FILTER = "All";

        private readonly BadgesDetails_PassportModuleView view;
        private readonly BadgeInfo_PassportModuleView badgeInfoModuleView;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly PassportErrorsController passportErrorsController;

        private readonly IObjectPool<ButtonWithSelectableStateView> badgesFilterButtonsPool;
        private readonly List<ButtonWithSelectableStateView> instantiatedBadgesFilterButtons = new ();
        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> badgeDetailCardsPool;
        private readonly List<BadgeDetailCard_PassportFieldView> instantiatedBadgeDetailCards = new ();
        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> emptyItemsPool;
        private readonly List<BadgeDetailCard_PassportFieldView> instantiatedEmptyItems = new ();

        private Profile currentProfile;
        private string? currentFilter;
        private CancellationTokenSource fetchBadgesCts;
        private CancellationTokenSource fetchBadgeCategoriesCts;

        public BadgesDetails_PassportModuleController(
            BadgesDetails_PassportModuleView view,
            BadgeInfo_PassportModuleView badgeInfoModuleView,
            BadgesAPIClient badgesAPIClient,
            PassportErrorsController passportErrorsController)
        {
            this.view = view;
            this.badgeInfoModuleView = badgeInfoModuleView;
            this.badgesAPIClient = badgesAPIClient;
            this.passportErrorsController = passportErrorsController;

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

            fetchBadgeCategoriesCts = fetchBadgeCategoriesCts.SafeRestart();
            LoadBadgeCategoriesAsync(fetchBadgeCategoriesCts.Token).Forget();
        }

        private async UniTaskVoid LoadBadgeCategoriesAsync(CancellationToken ct)
        {
            try
            {
                var badgeCategories = await BadgesAPIClient.FetchBadgeCategoriesAsync(ct);
                foreach (string category in badgeCategories)
                    CreateFilterButton(category);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading badges. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void CreateFilterButton(string badgeCategory)
        {
            var allBadgesFilterButton = badgesFilterButtonsPool.Get();
            allBadgesFilterButton.SetSelected(badgeCategory == ALL_FILTER);
            allBadgesFilterButton.Text.text = badgeCategory;
            allBadgesFilterButton.Button.onClick.AddListener(() => OnBadgesFilterButtonClicked(badgeCategory));
            instantiatedBadgesFilterButtons.Add(allBadgesFilterButton);
        }

        private void OnBadgesFilterButtonClicked(string categoryFilter)
        {
            if (currentFilter == categoryFilter)
                return;

            foreach (ButtonWithSelectableStateView filterButton in instantiatedBadgesFilterButtons)
                filterButton.SetSelected(filterButton.Text.text == categoryFilter);

            ShowBadgesInGridByCategory(categoryFilter);
        }

        private void LoadBadgeDetailCards()
        {
            ClearBadgeDetailCards();
            badgeInfoModuleView.SetAsLoading(true);

            if (string.IsNullOrEmpty(currentProfile.UserId))
                return;

            fetchBadgesCts = fetchBadgesCts.SafeRestart();
            LoadBadgeDetailCardsAsync(currentProfile.UserId, fetchBadgesCts.Token).Forget();
        }

        private async UniTaskVoid LoadBadgeDetailCardsAsync(string walletId, CancellationToken ct)
        {
            try
            {
                var badges = await badgesAPIClient.FetchBadgesAsync(walletId, true, 0, 0, ct);

                foreach (var unlockedBadge in badges.unlocked)
                    CreateBadgeDetailCard(unlockedBadge);

                foreach (var lockedBadge in badges.locked)
                    CreateBadgeDetailCard(lockedBadge);

                if (instantiatedBadgeDetailCards.Count > 0)
                {
                    instantiatedBadgeDetailCards[0].SetAsSelected(true);
                    badgeInfoModuleView.Setup(instantiatedBadgeDetailCards[0].Model);
                    badgeInfoModuleView.SetAsLoading(false);
                }

                int missingEmptyItems = CalculateMissingEmptyItems(instantiatedBadgeDetailCards.Count);
                for (var i = 0; i < missingEmptyItems; i++)
                {
                    var emptyItem = emptyItemsPool.Get();
                    emptyItem.gameObject.name = "EmptyItem";
                    instantiatedEmptyItems.Add(emptyItem);
                }

                ShowBadgesInGridByCategory(ALL_FILTER);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading badges. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void CreateBadgeDetailCard(BadgeInfo badge)
        {
            var badgeDetailCard = badgeDetailCardsPool.Get();
            badgeDetailCard.Setup(badge);

            badgeDetailCard.Button.onClick.AddListener(() =>
            {
                foreach (BadgeDetailCard_PassportFieldView instantiatedBadge in instantiatedBadgeDetailCards)
                    instantiatedBadge.SetAsSelected(false);

                badgeDetailCard.SetAsSelected(true);
                badgeInfoModuleView.Setup(badge);
            });

            instantiatedBadgeDetailCards.Add(badgeDetailCard);
        }

        private static int CalculateMissingEmptyItems(int totalItems)
        {
            int remainder = totalItems % GRID_ITEMS_PER_ROW;
            int missingItems = remainder == 0 ? 0 : GRID_ITEMS_PER_ROW - remainder;
            return missingItems;
        }

        private void ShowBadgesInGridByCategory(string category)
        {
            currentFilter = category;
        }

        private void ClearBadgesFilterButtons()
        {
            fetchBadgeCategoriesCts.SafeCancelAndDispose();

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
            fetchBadgesCts.SafeCancelAndDispose();

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
