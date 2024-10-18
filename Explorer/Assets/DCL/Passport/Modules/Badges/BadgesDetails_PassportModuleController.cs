using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.Passport.Fields.Badges;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Passport.Modules.Badges
{
    public class BadgesDetails_PassportModuleController : IPassportModuleController
    {
        private const string ALL_FILTER = "All";

        public event Action<string> OnBadgeSelected;

        private readonly BadgesDetails_PassportModuleView view;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly PassportErrorsController passportErrorsController;
        private readonly ISelfProfile selfProfile;
        private readonly BadgesCategories_PassportModuleSubController badgesCategoriesController;
        private readonly BadgeInfo_PassportModuleSubController badgeInfoController;
        private readonly BadgeDetailsCards_PassportModuleSubController badgeDetailsCardsController;

        private Profile currentProfile;
        private string? currentDefaultBadgeId;
        private bool isOwnProfile;
        private List<string> badgeCategories;
        private CancellationTokenSource fetchBadgesCts;
        private CancellationTokenSource fetchTiersCts;
        private CancellationTokenSource checkProfileCts;

        public BadgesDetails_PassportModuleController(
            BadgesDetails_PassportModuleView view,
            BadgeInfo_PassportModuleView badgeInfoModuleView,
            BadgesAPIClient badgesAPIClient,
            PassportErrorsController passportErrorsController,
            IWebRequestController webRequestController,
            ISelfProfile selfProfile)
        {
            this.view = view;
            this.badgesAPIClient = badgesAPIClient;
            this.passportErrorsController = passportErrorsController;
            this.selfProfile = selfProfile;

            badgesCategoriesController = new BadgesCategories_PassportModuleSubController(view);
            badgeInfoController = new BadgeInfo_PassportModuleSubController(badgeInfoModuleView, webRequestController, badgesAPIClient, passportErrorsController);
            badgeDetailsCardsController = new BadgeDetailsCards_PassportModuleSubController(view, webRequestController, badgesCategoriesController, badgeInfoController);

            badgeDetailsCardsController.OnBadgeSelected += BadgeSelected;
            badgesCategoriesController.OnBadgesFilterButtonClicked += OnBadgesCategoryButtonClicked;
        }

        private void BadgeSelected(string badgeId)
        {
            OnBadgeSelected?.Invoke(badgeId);
        }

        public void SetBadgeByDefault(string badgeId) =>
            currentDefaultBadgeId = badgeId;

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            checkProfileCts = checkProfileCts.SafeRestart();
            CheckProfileAndLoadBadgesAsync(checkProfileCts.Token).Forget();
        }

        public void Clear()
        {
            checkProfileCts.SafeCancelAndDispose();
            fetchBadgesCts.SafeCancelAndDispose();

            badgeDetailsCardsController.ClearBadgeDetailCards();
            badgesCategoriesController.ClearBadgesFilterButtons();
            badgesCategoriesController.ClearBadgesCategorySeparators();
            badgesCategoriesController.ClearBadgesCategoryContainers();
            badgeInfoController.Clear();
        }

        public void Dispose()
        {
            badgesCategoriesController.OnBadgesFilterButtonClicked -= OnBadgesCategoryButtonClicked;
            Clear();
        }

        private async UniTaskVoid CheckProfileAndLoadBadgesAsync(CancellationToken ct)
        {
            var ownProfile = await selfProfile.ProfileAsync(ct);
            isOwnProfile = ownProfile?.UserId == currentProfile.UserId;
            LoadBadgeDetailCards();
        }

        private void LoadBadgeDetailCards()
        {
            Clear();
            badgeInfoController.SetAsLoading(true);
            badgeInfoController.SetAsEmpty(false);
            view.NoBadgesLabel.SetActive(false);

            if (string.IsNullOrEmpty(currentProfile.UserId))
                return;

            fetchBadgesCts = fetchBadgesCts.SafeRestart();
            LoadBadgeDetailCardsAsync(currentProfile.UserId, fetchBadgesCts.Token).Forget();
        }

        private async UniTaskVoid LoadBadgeDetailCardsAsync(string walletId, CancellationToken ct)
        {
            try
            {
                badgesCategoriesController.CreateFilterButton(ALL_FILTER);
                badgesCategoriesController.CurrentFilter = ALL_FILTER;
                await LoadBadgeCategoriesAsync(ct);

                view.LoadingSpinner.SetActive(true);
                var badges = await badgesAPIClient.FetchBadgesAsync(walletId, isOwnProfile, ct);

                foreach (var unlockedBadge in badges.achieved)
                    badgeDetailsCardsController.CreateBadgeDetailCard(unlockedBadge, isOwnProfile);

                if (isOwnProfile)
                {
                    foreach (var lockedBadge in badges.notAchieved)
                        badgeDetailsCardsController.CreateBadgeDetailCard(lockedBadge, isOwnProfile);
                }

                ActivateOnlyCategoriesInUse();
                badgeDetailsCardsController.CreateEmptyDetailCards();
                ShowBadgesInGridByCategory(ALL_FILTER);
                view.LoadingSpinner.SetActive(false);
                badgeInfoController.SetAsEmpty(badges.achieved.Count == 0 && badges.notAchieved.Count == 0);
                view.NoBadgesLabel.SetActive(badges.achieved.Count == 0 && badges.notAchieved.Count == 0);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading badges. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.BADGES, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private async UniTask LoadBadgeCategoriesAsync(CancellationToken ct)
        {
            try
            {
                badgeCategories = await badgesAPIClient.FetchBadgeCategoriesAsync(ct);

                foreach (string category in badgeCategories)
                {
                    badgesCategoriesController.CreateFilterButton(category);
                    badgesCategoriesController.CreateCategorySeparator(category);
                    badgesCategoriesController.CreateCategoryContainer(category);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading badges. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.BADGES, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void ActivateOnlyCategoriesInUse()
        {
            var numberOfActiveSeparators = 0;
            badgesCategoriesController.InstantiatedBadgesFilterButtons[0].gameObject.SetActive(true);
            foreach (var badgesCategorySeparator in badgesCategoriesController.InstantiatedBadgesCategorySeparators)
            {
                if (!badgeDetailsCardsController.InstantiatedBadgeDetailCards.TryGetValue(badgesCategorySeparator.CategoryText.text.ToLower(), out List<BadgeDetailCard_PassportFieldView> badgeDetailCards))
                    continue;

                if (badgeDetailCards.Count > 0)
                {
                    badgesCategorySeparator.gameObject.SetActive(true);
                    numberOfActiveSeparators++;
                }
                else
                {
                    badgesCategorySeparator.gameObject.SetActive(false);
                    continue;
                }

                foreach (var filterButton in badgesCategoriesController.InstantiatedBadgesFilterButtons)
                {
                    if (!string.Equals(filterButton.Text.text, badgesCategorySeparator.CategoryText.text, StringComparison.OrdinalIgnoreCase))
                        continue;

                    filterButton.gameObject.SetActive(true);
                    break;
                }
            }

            view.BadgesFilterButtonsContainer.gameObject.SetActive(numberOfActiveSeparators > 1);
        }

        private void ShowBadgesInGridByCategory(string category)
        {
            badgesCategoriesController.CurrentFilter = category;

            foreach (var badgesCategorySeparator in badgesCategoriesController.InstantiatedBadgesCategorySeparators)
                badgesCategorySeparator.gameObject.SetActive(category == ALL_FILTER && badgeDetailsCardsController.InstantiatedBadgeDetailCards.ContainsKey(badgesCategorySeparator.CategoryText.text.ToLower()));

            foreach (var badgesCategoryContainer in badgesCategoriesController.InstantiatedBadgesCategoryContainers)
                badgesCategoryContainer.gameObject.SetActive(category == ALL_FILTER ?
                    badgeDetailsCardsController.InstantiatedBadgeDetailCards.ContainsKey(badgesCategoryContainer.Category.ToLower()) :
                    badgesCategoryContainer.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(currentDefaultBadgeId))
                SelectFirstBadge();
            else
                SelectSpecificBadge();
        }

        private void SelectFirstBadge()
        {
            var firstElementSelected = false;
            BadgeDetailCard_PassportFieldView? cardToSelect = null;
            foreach (string? category in badgeCategories)
            {
                if (badgesCategoriesController.CurrentFilter != ALL_FILTER && !string.Equals(category, badgesCategoriesController.CurrentFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!badgeDetailsCardsController.InstantiatedBadgeDetailCards.TryGetValue(category.ToLower(), out List<BadgeDetailCard_PassportFieldView> badgeDetailCards))
                    continue;

                if (badgeDetailCards.Count == 0)
                    continue;

                foreach (var badgeDetailCard in badgeDetailCards)
                {
                    badgeDetailCard.SetAsSelected(false);
                    if (!firstElementSelected)
                        cardToSelect = badgeDetailCard;

                    firstElementSelected = true;
                }
            }

            if (cardToSelect != null)
                badgeDetailsCardsController.SelectBadgeCard(cardToSelect, isOwnProfile);
        }

        private void SelectSpecificBadge()
        {
            if (string.IsNullOrEmpty(currentDefaultBadgeId))
            {
                SelectFirstBadge();
                return;
            }

            BadgeDetailCard_PassportFieldView? cardToSelect = null;
            foreach (var badgeDetailCard in badgeDetailsCardsController.InstantiatedBadgeDetailCards)
            {
                foreach (var badgeCard in badgeDetailCard.Value)
                {
                    if (badgeCard.Model.data.id == currentDefaultBadgeId)
                    {
                        badgeCard.SetAsSelected(false);
                        cardToSelect = badgeCard;
                    }
                }
            }

            if (cardToSelect != null)
                badgeDetailsCardsController.SelectBadgeCard(cardToSelect, isOwnProfile);
            else
                SelectFirstBadge();

            currentDefaultBadgeId = null;
        }

        private void OnBadgesCategoryButtonClicked(string categoryFilter)
        {
            if (badgesCategoriesController.CurrentFilter == categoryFilter)
                return;

            foreach (ButtonWithSelectableStateView filterButton in badgesCategoriesController.InstantiatedBadgesFilterButtons)
                filterButton.SetSelected(filterButton.Text.text == categoryFilter);

            ShowBadgesInGridByCategory(categoryFilter);
        }
    }
}
