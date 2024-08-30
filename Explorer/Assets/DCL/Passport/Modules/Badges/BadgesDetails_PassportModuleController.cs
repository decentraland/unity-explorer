using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.Passport.Fields.Badges;
using DCL.Passport.Utils;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules.Badges
{
    public class BadgesDetails_PassportModuleController : IPassportModuleController
    {
        private const int BADGES_CATEGORIES_POOL_DEFAULT_CAPACITY = 6;
        private const int BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY = 50;
        private const int GRID_ITEMS_PER_ROW = 6;
        private const string ALL_FILTER = "All";

        private readonly BadgesDetails_PassportModuleView view;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly PassportErrorsController passportErrorsController;
        private readonly ISelfProfile selfProfile;
        private readonly BadgeInfo_PassportModuleSubController badgeInfoController;
        private readonly IObjectPool<ButtonWithSelectableStateView> badgesFilterButtonsPool;
        private readonly List<ButtonWithSelectableStateView> instantiatedBadgesFilterButtons = new ();
        private readonly IObjectPool<BadgesCategorySeparator_PassportFieldView> badgesCategorySeparatorsPool;
        private readonly List<BadgesCategorySeparator_PassportFieldView> instantiatedBadgesCategorySeparators = new ();
        private readonly IObjectPool<BadgesCategoryContainer_PassportFieldView> badgesCategoryContainersPool;
        private readonly List<BadgesCategoryContainer_PassportFieldView> instantiatedBadgesCategoryContainers = new ();
        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> badgeDetailCardsPool;
        private readonly Dictionary<string,List<BadgeDetailCard_PassportFieldView>> instantiatedBadgeDetailCards = new ();
        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> emptyItemsPool;
        private readonly List<BadgeDetailCard_PassportFieldView> instantiatedEmptyItems = new ();

        private Profile currentProfile;
        private string? currentBadgeIdToSelect;
        private bool isOwnProfile;
        private string? currentFilter;
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

            badgesFilterButtonsPool = new ObjectPool<ButtonWithSelectableStateView>(
                InstantiateBadgesFilterButtonPrefab,
                defaultCapacity: BADGES_CATEGORIES_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgesFilterButton =>
                {
                    badgesFilterButton.gameObject.SetActive(false);
                    badgesFilterButton.gameObject.transform.SetAsLastSibling();
                },
                actionOnRelease: badgesFilterButton => badgesFilterButton.gameObject.SetActive(false));

            badgesCategorySeparatorsPool = new ObjectPool<BadgesCategorySeparator_PassportFieldView>(
                InstantiateBadgesCategorySeparatorPrefab,
                defaultCapacity: BADGES_CATEGORIES_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgesCategorySeparator =>
                {
                    badgesCategorySeparator.gameObject.SetActive(false);
                    badgesCategorySeparator.gameObject.transform.SetAsLastSibling();
                },
                actionOnRelease: badgesCategorySeparator => badgesCategorySeparator.gameObject.SetActive(false));

            badgesCategoryContainersPool = new ObjectPool<BadgesCategoryContainer_PassportFieldView>(
                InstantiateBadgesCategoryContainerPrefab,
                defaultCapacity: BADGES_CATEGORIES_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgesCategoryContainer => badgesCategoryContainer.gameObject.SetActive(false),
                actionOnRelease: badgesCategoryContainer => badgesCategoryContainer.gameObject.SetActive(false));

            badgeDetailCardsPool = new ObjectPool<BadgeDetailCard_PassportFieldView>(
                InstantiateBadgeDetailCardPrefab,
                defaultCapacity: BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgeDetailCardView =>
                {
                    badgeDetailCardView.ConfigureImageController(webRequestController);
                    badgeDetailCardView.gameObject.SetActive(true);
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
                },
                actionOnRelease: emptyItemView => emptyItemView.gameObject.SetActive(false));

            badgeInfoController = new BadgeInfo_PassportModuleSubController(badgeInfoModuleView, webRequestController, badgesAPIClient, passportErrorsController);
        }

        public void SetBadgeToSelect(string badgeId) =>
            currentBadgeIdToSelect = badgeId;

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            checkProfileCts = checkProfileCts.SafeRestart();
            CheckProfileAndLoadBadgesAsync(checkProfileCts.Token).Forget();
        }

        public void Clear()
        {
            ClearBadgesFilterButtons();
            ClearBadgeDetailCards();
            ClearBadgesCategorySeparators();
            ClearBadgesCategoryContainers();
            badgeInfoController.Clear();
        }

        public void Dispose() =>
            Clear();

        private ButtonWithSelectableStateView InstantiateBadgesFilterButtonPrefab()
        {
            ButtonWithSelectableStateView badgesFilterButton = Object.Instantiate(view.BadgesFilterButtonPrefab, view.BadgesFilterButtonsContainer);
            return badgesFilterButton;
        }

        private BadgesCategorySeparator_PassportFieldView InstantiateBadgesCategorySeparatorPrefab()
        {
            BadgesCategorySeparator_PassportFieldView badgesCategorySeparator = Object.Instantiate(view.BadgesCategorySeparatorPrefab, view.MainContainer);
            return badgesCategorySeparator;
        }

        private BadgesCategoryContainer_PassportFieldView InstantiateBadgesCategoryContainerPrefab()
        {
            BadgesCategoryContainer_PassportFieldView badgesCategoryContainer = Object.Instantiate(view.BadgesCategoryContainerPrefab, view.MainContainer);
            return badgesCategoryContainer;
        }

        private BadgeDetailCard_PassportFieldView InstantiateBadgeDetailCardPrefab()
        {
            BadgeDetailCard_PassportFieldView badgeDetailCareView = Object.Instantiate(view.BadgeDetailCardPrefab, view.MainContainer);
            return badgeDetailCareView;
        }

        private async UniTask LoadBadgeCategoriesAsync(CancellationToken ct)
        {
            try
            {
                badgeCategories = await badgesAPIClient.FetchBadgeCategoriesAsync(ct);

                foreach (string category in badgeCategories)
                {
                    CreateFilterButton(category);
                    CreateCategorySeparator(category);
                    CreateCategoryContainer(category);
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

        private void CreateFilterButton(string badgeCategory)
        {
            var badgeFilterButton = badgesFilterButtonsPool.Get();
            badgeFilterButton.SetSelected(badgeCategory == ALL_FILTER);
            badgeFilterButton.Text.text = badgeCategory;
            badgeFilterButton.Button.onClick.AddListener(() => OnBadgesFilterButtonClicked(badgeCategory));
            instantiatedBadgesFilterButtons.Add(badgeFilterButton);
        }

        private void CreateCategorySeparator(string badgeCategory)
        {
            var badgesCategorySeparator = badgesCategorySeparatorsPool.Get();
            badgesCategorySeparator.gameObject.name = $"Separator_{badgeCategory.ToUpper()}";
            badgesCategorySeparator.CategoryText.text = badgeCategory.ToUpper();
            instantiatedBadgesCategorySeparators.Add(badgesCategorySeparator);
        }

        private void CreateCategoryContainer(string badgeCategory)
        {
            var badgesCategoryContainer = badgesCategoryContainersPool.Get();
            badgesCategoryContainer.gameObject.name = $"Container_{badgeCategory.ToUpper()}";
            badgesCategoryContainer.Category = badgeCategory;

            // Place category container under the corresponding separator
            foreach (var badgesCategorySeparator in instantiatedBadgesCategorySeparators)
            {
                if (!string.Equals(badgesCategorySeparator.CategoryText.text, badgeCategory, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                badgesCategoryContainer.transform.SetSiblingIndex(badgesCategorySeparator.transform.GetSiblingIndex() + 1);
                break;
            }

            instantiatedBadgesCategoryContainers.Add(badgesCategoryContainer);
        }

        private void OnBadgesFilterButtonClicked(string categoryFilter)
        {
            if (currentFilter == categoryFilter)
                return;

            foreach (ButtonWithSelectableStateView filterButton in instantiatedBadgesFilterButtons)
                filterButton.SetSelected(filterButton.Text.text == categoryFilter);

            ShowBadgesInGridByCategory(categoryFilter);
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

            if (string.IsNullOrEmpty(currentProfile.UserId))
                return;

            fetchBadgesCts = fetchBadgesCts.SafeRestart();
            LoadBadgeDetailCardsAsync(currentProfile.UserId, fetchBadgesCts.Token).Forget();
        }

        private async UniTaskVoid LoadBadgeDetailCardsAsync(string walletId, CancellationToken ct)
        {
            try
            {
                CreateFilterButton(ALL_FILTER);
                currentFilter = ALL_FILTER;
                await LoadBadgeCategoriesAsync(ct);

                view.LoadingSpinner.SetActive(true);
                var badges = await badgesAPIClient.FetchBadgesAsync(walletId, isOwnProfile, ct);

                foreach (var unlockedBadge in badges.achieved)
                    CreateBadgeDetailCard(unlockedBadge);

                if (isOwnProfile)
                {
                    foreach (var lockedBadge in badges.notAchieved)
                        CreateBadgeDetailCard(lockedBadge);
                }

                ActivateOnlyCategoriesInUse();
                CreateEmptyDetailCards();
                ShowBadgesInGridByCategory(ALL_FILTER);
                view.LoadingSpinner.SetActive(false);
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
            instantiatedBadgesFilterButtons[0].gameObject.SetActive(true);
            foreach (var badgesCategorySeparator in instantiatedBadgesCategorySeparators)
            {
                if (!instantiatedBadgeDetailCards.TryGetValue(badgesCategorySeparator.CategoryText.text.ToLower(), out List<BadgeDetailCard_PassportFieldView> badgeDetailCards))
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

                foreach (var filterButton in instantiatedBadgesFilterButtons)
                {
                    if (!string.Equals(filterButton.Text.text, badgesCategorySeparator.CategoryText.text, StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    filterButton.gameObject.SetActive(true);
                    break;
                }
            }

            view.BadgesFilterButtonsContainer.gameObject.SetActive(numberOfActiveSeparators > 1);
        }

        private void SelectFirstBadge()
        {
            var firstElementSelected = false;
            BadgeDetailCard_PassportFieldView? cardToSelect = null;
            foreach (string? category in badgeCategories)
            {
                if (currentFilter != ALL_FILTER && !string.Equals(category, currentFilter, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                if (!instantiatedBadgeDetailCards.TryGetValue(category.ToLower(), out List<BadgeDetailCard_PassportFieldView> badgeDetailCards))
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
                SelectBadgeCard(cardToSelect);
        }

        private void SelectSpecificBadge()
        {
            if (string.IsNullOrEmpty(currentBadgeIdToSelect))
            {
                SelectFirstBadge();
                return;
            }

            BadgeDetailCard_PassportFieldView? cardToSelect = null;
            foreach (var badgeDetailCard in instantiatedBadgeDetailCards)
            {
                foreach (var badgeCard in badgeDetailCard.Value)
                {
                    if (badgeCard.Model.id == currentBadgeIdToSelect)
                    {
                        badgeCard.SetAsSelected(false);
                        cardToSelect = badgeCard;
                    }
                }
            }

            if (cardToSelect != null)
                SelectBadgeCard(cardToSelect);
            else
                SelectFirstBadge();

            currentBadgeIdToSelect = null;
        }

        private void CreateBadgeDetailCard(BadgeInfo badge)
        {
            if (isOwnProfile)
                badge.isNew = BadgesUtils.IsBadgeNew(badge.id);

            var badgeDetailCard = badgeDetailCardsPool.Get();
            badgeDetailCard.Setup(badge, isOwnProfile);
            badgeDetailCard.Button.onClick.AddListener(() => { SelectBadgeCard(badgeDetailCard); });

            // Place badge into the corresponding category container
            foreach (var badgesCategoryContainer in instantiatedBadgesCategoryContainers)
            {
                if (!string.Equals(badgesCategoryContainer.Category, badge.category, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                badgeDetailCard.transform.parent = badgesCategoryContainer.BadgeDetailCardsContainer;
                badgeDetailCard.transform.SetAsFirstSibling();
                break;
            }

            if (!instantiatedBadgeDetailCards.ContainsKey(badge.category.ToLower()))
                instantiatedBadgeDetailCards.Add(badge.category.ToLower(), new List<BadgeDetailCard_PassportFieldView>());

            instantiatedBadgeDetailCards[badge.category.ToLower()].Add(badgeDetailCard);
        }

        private void SelectBadgeCard(BadgeDetailCard_PassportFieldView badgeDetailCard)
        {
            if (badgeDetailCard.IsSelected)
                return;

            foreach (var instantiatedBadge in instantiatedBadgeDetailCards)
                foreach (var instantiateBadgeByCategory in instantiatedBadge.Value)
                    instantiateBadgeByCategory.SetAsSelected(false);

            badgeDetailCard.SetAsSelected(true);
            badgeInfoController.Setup(badgeDetailCard.Model, isOwnProfile);

            if (!badgeDetailCard.Model.isLocked && isOwnProfile)
            {
                BadgesUtils.SetBadgeAsRead(badgeDetailCard.Model.id);
                badgeDetailCard.SetAsNew(false);
            }
        }

        private void CreateEmptyDetailCards()
        {
            foreach (var badgeDetailCardsByCategory in instantiatedBadgeDetailCards)
            {
                int missingEmptyItems = CalculateMissingEmptyItems(badgeDetailCardsByCategory.Value.Count);
                for (var i = 0; i < missingEmptyItems; i++)
                {
                    var emptyItem = emptyItemsPool.Get();
                    emptyItem.gameObject.name = "EmptyItem";
                    emptyItem.transform.parent = badgeDetailCardsByCategory.Value[0].transform.parent;
                    emptyItem.transform.SetAsFirstSibling();
                    instantiatedEmptyItems.Add(emptyItem);
                }
            }
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

            foreach (var badgesCategorySeparator in instantiatedBadgesCategorySeparators)
                badgesCategorySeparator.gameObject.SetActive(category == ALL_FILTER && instantiatedBadgeDetailCards.ContainsKey(badgesCategorySeparator.CategoryText.text.ToLower()));

            foreach (var badgesCategoryContainer in instantiatedBadgesCategoryContainers)
                badgesCategoryContainer.gameObject.SetActive(category == ALL_FILTER ?
                    instantiatedBadgeDetailCards.ContainsKey(badgesCategoryContainer.Category.ToLower()) :
                    badgesCategoryContainer.Category.Equals(category, StringComparison.CurrentCultureIgnoreCase));

            if (string.IsNullOrEmpty(currentBadgeIdToSelect))
                SelectFirstBadge();
            else
                SelectSpecificBadge();
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
            checkProfileCts.SafeCancelAndDispose();
            fetchBadgesCts.SafeCancelAndDispose();

            ClearEmptyItems();

            foreach (var badgeDetailCards in instantiatedBadgeDetailCards)
            {
                foreach (var badgeDetailCardByCategory in badgeDetailCards.Value)
                {
                    badgeDetailCardByCategory.StopLoadingImage();
                    badgeDetailCardByCategory.Button.onClick.RemoveAllListeners();
                    badgeDetailCardsPool.Release(badgeDetailCardByCategory);
                }
            }

            instantiatedBadgeDetailCards.Clear();
        }

        private void ClearEmptyItems()
        {
            foreach (BadgeDetailCard_PassportFieldView emptyItem in instantiatedEmptyItems)
                emptyItemsPool.Release(emptyItem);

            instantiatedEmptyItems.Clear();
        }

        private void ClearBadgesCategorySeparators()
        {
            foreach (BadgesCategorySeparator_PassportFieldView badgesCategorySeparator in instantiatedBadgesCategorySeparators)
                badgesCategorySeparatorsPool.Release(badgesCategorySeparator);

            instantiatedBadgesCategorySeparators.Clear();
        }

        private void ClearBadgesCategoryContainers()
        {
            foreach (BadgesCategoryContainer_PassportFieldView badgesCategoryContainer in instantiatedBadgesCategoryContainers)
                badgesCategoryContainersPool.Release(badgesCategoryContainer);

            instantiatedBadgesCategoryContainers.Clear();
        }
    }
}
