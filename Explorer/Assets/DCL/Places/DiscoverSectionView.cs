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
        private readonly List<ButtonWithSelectableStateView> currentCategories = new ();

        private void Awake()
        {
            categoryButtonsPool = new ObjectPool<ButtonWithSelectableStateView>(
                InstantiateCategoryButtonPrefab,
                defaultCapacity: CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY,
                actionOnGet: categoryButtonView => categoryButtonView.gameObject.SetActive(true),
                actionOnRelease: categoryButtonView => categoryButtonView.gameObject.SetActive(false));
        }

        public void SetActive(bool active) =>
            gameObject.SetActive(active);

        public void SetCategories(string[] categories)
        {
            foreach (string categoryName in categories)
                CreateAndSetupCategoryButton(categoryName);
        }

        public void ClearCategories()
        {
            foreach (var categoryButton in currentCategories)
                categoryButtonsPool.Release(categoryButton);

            currentCategories.Clear();
        }

        private ButtonWithSelectableStateView InstantiateCategoryButtonPrefab()
        {
            ButtonWithSelectableStateView invitedCommunityCardView = Instantiate(categoryButtonPrefab, categoriesContainer);
            return invitedCommunityCardView;
        }

        private void CreateAndSetupCategoryButton(string categoryName)
        {
            ButtonWithSelectableStateView categoryButtonView = categoryButtonsPool.Get();

            // Setup card data
            categoryButtonView.Text.text = categoryName;

            // Setup card events
            categoryButtonView.Button.onClick.RemoveAllListeners();
            categoryButtonView.Button.onClick.AddListener(() => CategorySelected?.Invoke(categoryName));

            currentCategories.Add(categoryButtonView);
        }
    }
}
