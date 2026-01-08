using DCL.PlacesAPIService;
using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Places
{
    public class DiscoverSectionView : MonoBehaviour
    {
        public event Action<string>? CategorySelected;

        [Header("Categories")]
        [SerializeField] private ButtonWithSelectableStateView categoryButtonPrefab = null!;
        [SerializeField] private Transform categoriesContainer = null!;

        private const int CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY = 15;

        private IObjectPool<ButtonWithSelectableStateView> categoryButtonsPool = null!;
        private readonly List<KeyValuePair<string, ButtonWithSelectableStateView>> currentCategories = new ();

        private void Awake()
        {
            categoryButtonsPool = new ObjectPool<ButtonWithSelectableStateView>(
                InstantiateCategoryButtonPrefab,
                defaultCapacity: CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY,
                actionOnGet: categoryButtonView =>
                {
                    categoryButtonView.gameObject.SetActive(true);
                    categoryButtonView.transform.SetAsLastSibling();
                },
                actionOnRelease: categoryButtonView => categoryButtonView.gameObject.SetActive(false));
        }

        public void SetActive(bool active) =>
            gameObject.SetActive(active);

        public void SetCategories(PlaceCategoryData[] categories)
        {
            var allCategoryData = new PlaceCategoryData
            {
                name = "all",
                i18n = new PlaceCategoryLocalizationData
                {
                    en = "All",
                },
            };

            CreateAndSetupCategoryButton(allCategoryData);

            foreach (PlaceCategoryData categoryData in categories)
                CreateAndSetupCategoryButton(categoryData);

            SelectCategory(allCategoryData.name);
        }

        public void ClearCategories()
        {
            foreach (var categoryButton in currentCategories)
                categoryButtonsPool.Release(categoryButton.Value);

            currentCategories.Clear();
        }

        private ButtonWithSelectableStateView InstantiateCategoryButtonPrefab()
        {
            ButtonWithSelectableStateView invitedCommunityCardView = Instantiate(categoryButtonPrefab, categoriesContainer);
            return invitedCommunityCardView;
        }

        private void CreateAndSetupCategoryButton(PlaceCategoryData categoryData)
        {
            ButtonWithSelectableStateView categoryButtonView = categoryButtonsPool.Get();

            // Setup card data
            categoryButtonView.Text.text = categoryData.i18n.en;

            // Setup card events
            categoryButtonView.Button.onClick.RemoveAllListeners();
            categoryButtonView.Button.onClick.AddListener(() => SelectCategory(categoryData.name));

            currentCategories.Add(new KeyValuePair<string, ButtonWithSelectableStateView>(categoryData.name, categoryButtonView));
        }

        private void SelectCategory(string categoryId)
        {
            foreach (var category in currentCategories)
                category.Value.SetSelected(category.Key == categoryId);

            CategorySelected?.Invoke(categoryId);
        }
    }
}
