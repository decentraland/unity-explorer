using DCL.Passport.Fields.Badges;
using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules.Badges
{
    public class BadgesCategories_PassportModuleSubController
    {
        private const string ALL_FILTER = "All";
        private const int BADGES_CATEGORIES_POOL_DEFAULT_CAPACITY = 6;

        public readonly List<ButtonWithSelectableStateView> InstantiatedBadgesFilterButtons = new ();
        public readonly List<BadgesCategorySeparator_PassportFieldView> InstantiatedBadgesCategorySeparators = new ();
        public readonly List<BadgesCategoryContainer_PassportFieldView> InstantiatedBadgesCategoryContainers = new ();
        public event Action<string> OnBadgesFilterButtonClicked;
        public string? CurrentFilter;

        private readonly BadgesDetails_PassportModuleView view;
        private readonly IObjectPool<ButtonWithSelectableStateView> badgesFilterButtonsPool;
        private readonly IObjectPool<BadgesCategorySeparator_PassportFieldView> badgesCategorySeparatorsPool;
        private readonly IObjectPool<BadgesCategoryContainer_PassportFieldView> badgesCategoryContainersPool;

        public BadgesCategories_PassportModuleSubController(BadgesDetails_PassportModuleView view)
        {
            this.view = view;

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
        }

        public void CreateFilterButton(string badgeCategory)
        {
            var badgeFilterButton = badgesFilterButtonsPool.Get();
            badgeFilterButton.SetSelected(badgeCategory == ALL_FILTER);
            badgeFilterButton.Text.text = badgeCategory;
            badgeFilterButton.Button.onClick.AddListener(() => OnBadgesFilterButtonClicked.Invoke(badgeCategory));
            InstantiatedBadgesFilterButtons.Add(badgeFilterButton);
        }

        public void CreateCategorySeparator(string badgeCategory)
        {
            var badgesCategorySeparator = badgesCategorySeparatorsPool.Get();
            badgesCategorySeparator.gameObject.name = $"Separator_{badgeCategory.ToUpper()}";
            badgesCategorySeparator.CategoryText.text = badgeCategory.ToUpper();
            InstantiatedBadgesCategorySeparators.Add(badgesCategorySeparator);
        }

        public void CreateCategoryContainer(string badgeCategory)
        {
            var badgesCategoryContainer = badgesCategoryContainersPool.Get();
            badgesCategoryContainer.gameObject.name = $"Container_{badgeCategory.ToUpper()}";
            badgesCategoryContainer.Category = badgeCategory;

            // Place category container under the corresponding separator
            foreach (var badgesCategorySeparator in InstantiatedBadgesCategorySeparators)
            {
                if (!string.Equals(badgesCategorySeparator.CategoryText.text, badgeCategory, StringComparison.OrdinalIgnoreCase))
                    continue;

                badgesCategoryContainer.transform.SetSiblingIndex(badgesCategorySeparator.transform.GetSiblingIndex() + 1);
                break;
            }

            InstantiatedBadgesCategoryContainers.Add(badgesCategoryContainer);
        }

        public void ClearBadgesFilterButtons()
        {
            foreach (ButtonWithSelectableStateView badgesFilterButton in InstantiatedBadgesFilterButtons)
            {
                badgesFilterButton.Button.onClick.RemoveAllListeners();
                badgesFilterButtonsPool.Release(badgesFilterButton);
            }

            InstantiatedBadgesFilterButtons.Clear();
            CurrentFilter = null;
        }

        public void ClearBadgesCategorySeparators()
        {
            foreach (BadgesCategorySeparator_PassportFieldView badgesCategorySeparator in InstantiatedBadgesCategorySeparators)
                badgesCategorySeparatorsPool.Release(badgesCategorySeparator);

            InstantiatedBadgesCategorySeparators.Clear();
        }

        public void ClearBadgesCategoryContainers()
        {
            foreach (BadgesCategoryContainer_PassportFieldView badgesCategoryContainer in InstantiatedBadgesCategoryContainers)
                badgesCategoryContainersPool.Release(badgesCategoryContainer);

            InstantiatedBadgesCategoryContainers.Clear();
        }

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
    }
}
