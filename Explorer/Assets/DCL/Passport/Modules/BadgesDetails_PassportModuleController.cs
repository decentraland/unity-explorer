using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.Passport.Fields;
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

namespace DCL.Passport.Modules
{
    public class BadgesDetails_PassportModuleController : IPassportModuleController
    {
        private const int BADGES_CATEGORIES_POOL_DEFAULT_CAPACITY = 6;
        private const int BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY = 50;
        private const int GRID_ITEMS_PER_ROW = 6;
        private const string ALL_FILTER = "All";

        private readonly BadgesDetails_PassportModuleView view;
        private readonly BadgeInfo_PassportModuleView badgeInfoModuleView;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly PassportErrorsController passportErrorsController;
        private readonly ISelfProfile selfProfile;

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
        private bool isOwnProfile;
        private string? currentFilter;
        private List<string> badgeCategories;
        private CancellationTokenSource fetchBadgesCts;
        private CancellationTokenSource fetchBadgeCategoriesCts;
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
            this.badgeInfoModuleView = badgeInfoModuleView;
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

            badgeInfoModuleView.ConfigureImageController(webRequestController);
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            LoadBadgeCategories();

            checkProfileCts = checkProfileCts.SafeRestart();
            CheckProfileAndLoadBadgesAsync(checkProfileCts.Token).Forget();
        }

        public void Clear()
        {
            ClearBadgesFilterButtons();
            ClearBadgeDetailCards();
            ClearBadgesCategorySeparators();
            ClearBadgesCategoryContainers();
            badgeInfoModuleView.StopLoadingImage();
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

        private void LoadBadgeCategories()
        {
            ClearBadgesFilterButtons();
            ClearBadgesCategorySeparators();
            ClearBadgesCategoryContainers();
            CreateFilterButton(ALL_FILTER);
            currentFilter = ALL_FILTER;

            fetchBadgeCategoriesCts = fetchBadgeCategoriesCts.SafeRestart();
            LoadBadgeCategoriesAsync(fetchBadgeCategoriesCts.Token).Forget();
        }

        private async UniTaskVoid LoadBadgeCategoriesAsync(CancellationToken ct)
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
                view.LoadingSpinner.SetActive(true);
                var badges = await badgesAPIClient.FetchBadgesAsync(walletId, isOwnProfile, 0, ct);

                foreach (var unlockedBadge in badges.unlocked)
                    CreateBadgeDetailCard(unlockedBadge);

                foreach (var lockedBadge in badges.locked)
                    CreateBadgeDetailCard(lockedBadge);

                instantiatedBadgesFilterButtons[0].gameObject.SetActive(true);
                foreach (var badgesCategorySeparator in instantiatedBadgesCategorySeparators)
                {
                    if (!instantiatedBadgeDetailCards.TryGetValue(badgesCategorySeparator.CategoryText.text.ToLower(), out List<BadgeDetailCard_PassportFieldView> badgeDetailCards))
                        continue;

                    badgesCategorySeparator.gameObject.SetActive(badgeDetailCards.Count > 0);

                    if (badgeDetailCards.Count == 0)
                        continue;

                    foreach (var filterButton in instantiatedBadgesFilterButtons)
                    {
                        if (!string.Equals(filterButton.Text.text, badgesCategorySeparator.CategoryText.text, StringComparison.CurrentCultureIgnoreCase))
                            continue;

                        filterButton.gameObject.SetActive(true);
                        break;
                    }
                }

                CreateEmptyDetailCards();
                ShowBadgesInGridByCategory(ALL_FILTER);
                SelectFirstBadge();
                view.LoadingSpinner.SetActive(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading badges. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void SelectFirstBadge()
        {
            var firstElementSelected = false;
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
                    badgeDetailCard.SetAsSelected(!firstElementSelected);
                    if (!firstElementSelected)
                    {
                        badgeInfoModuleView.Setup(badgeDetailCard.Model, isOwnProfile);
                        badgeInfoModuleView.SetAsLoading(false);
                    }
                    firstElementSelected = true;
                }
            }
        }

        private void CreateBadgeDetailCard(BadgeInfo badge)
        {
            var badgeDetailCard = badgeDetailCardsPool.Get();
            badgeDetailCard.Setup(badge);

            badgeDetailCard.Button.onClick.AddListener(() =>
            {
                foreach (var instantiatedBadge in instantiatedBadgeDetailCards)
                    foreach (var instantiateBadgeByCategory in instantiatedBadge.Value)
                        instantiateBadgeByCategory.SetAsSelected(false);

                badgeDetailCard.SetAsSelected(true);
                badgeInfoModuleView.Setup(badge, isOwnProfile);
            });

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
            {
                if (category == ALL_FILTER)
                    badgesCategoryContainer.gameObject.SetActive(instantiatedBadgeDetailCards.ContainsKey(badgesCategoryContainer.Category.ToLower()));
                else
                    badgesCategoryContainer.gameObject.SetActive(badgesCategoryContainer.Category.Equals(category, StringComparison.CurrentCultureIgnoreCase));
            }

            SelectFirstBadge();
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
