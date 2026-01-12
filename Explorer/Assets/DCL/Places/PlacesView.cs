using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Places
{
    public class PlacesView : MonoBehaviour
    {
        private const int CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY = 15;
        private const string ALL_CATEGORY_ID = "all";

        public Action<PlacesSection>? SectionChanged;
        public event Action<string?>? CategorySelected;

        public PlacesResultsView DiscoverView => placesResultsView;

        private PlacesSection? currentSection;
        private IObjectPool<PlaceCategoryButton> categoryButtonsPool = null!;
        private readonly List<KeyValuePair<string, PlaceCategoryButton>> currentCategories = new ();

        [Header("Sections Tabs")]
        [SerializeField] private ButtonWithSelectableStateView discoverSectionTab = null!;
        [SerializeField] private ButtonWithSelectableStateView favoritesSectionTab = null!;
        [SerializeField] private ButtonWithSelectableStateView recentlyVisitedSectionTab = null!;
        [SerializeField] private ButtonWithSelectableStateView myPlacesSectionTab = null!;

        [Header("Categories")]
        [SerializeField] private PlaceCategoryButton categoryButtonPrefab = null!;
        [SerializeField] private Transform categoriesContainer = null!;

        [Header("Places Results")]
        [SerializeField] private PlacesResultsView placesResultsView = null!;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator = null!;
        [SerializeField] private Animator headerAnimator = null!;

        private void Awake()
        {
            discoverSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.DISCOVER));
            favoritesSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.FAVORITES));
            recentlyVisitedSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.RECENTLY_VISITED));
            myPlacesSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.MY_PLACES));

            categoryButtonsPool = new ObjectPool<PlaceCategoryButton>(
                InstantiateCategoryButtonPrefab,
                defaultCapacity: CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY,
                actionOnGet: categoryButtonView =>
                {
                    categoryButtonView.gameObject.SetActive(true);
                    categoryButtonView.transform.SetAsLastSibling();
                },
                actionOnRelease: categoryButtonView => categoryButtonView.gameObject.SetActive(false));
        }

        private void OnDestroy()
        {
            discoverSectionTab.Button.onClick.RemoveAllListeners();
            favoritesSectionTab.Button.onClick.RemoveAllListeners();
            recentlyVisitedSectionTab.Button.onClick.RemoveAllListeners();
            myPlacesSectionTab.Button.onClick.RemoveAllListeners();
        }

        public void SetViewActive(bool isActive) =>
            gameObject.SetActive(isActive);

        public void PlayAnimator(int triggerId)
        {
            panelAnimator.SetTrigger(triggerId);
            headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            panelAnimator.Rebind();
            headerAnimator.Rebind();
            panelAnimator.Update(0);
            headerAnimator.Update(0);
        }

        public void OpenSection(PlacesSection section, bool force = false)
        {
            if (currentSection == section && !force)
                return;

            discoverSectionTab.SetSelected(false);
            favoritesSectionTab.SetSelected(false);
            recentlyVisitedSectionTab.SetSelected(false);
            myPlacesSectionTab.SetSelected(false);

            switch (section)
            {
                case PlacesSection.DISCOVER:
                    discoverSectionTab.SetSelected(true);
                    break;
                case PlacesSection.FAVORITES:
                    favoritesSectionTab.SetSelected(true);
                    break;
                case PlacesSection.RECENTLY_VISITED:
                    recentlyVisitedSectionTab.SetSelected(true);
                    break;
                case PlacesSection.MY_PLACES:
                    myPlacesSectionTab.SetSelected(true);
                    break;
            }

            SectionChanged?.Invoke(section);
            currentSection = section;
        }

        public void SetCategories(PlaceCategoriesSO.PlaceCategoryData[] categories)
        {
            foreach (PlaceCategoriesSO.PlaceCategoryData categoryData in categories)
                CreateAndSetupCategoryButton(categoryData);

            SelectCategory(ALL_CATEGORY_ID, invokeEvent: false);
        }

        public void ClearCategories()
        {
            foreach (var categoryButton in currentCategories)
                categoryButtonsPool.Release(categoryButton.Value);

            currentCategories.Clear();
        }

        public void SetCategoriesVisible(bool isVisible) =>
            categoriesContainer.gameObject.SetActive(isVisible);

        private PlaceCategoryButton InstantiateCategoryButtonPrefab()
        {
            PlaceCategoryButton invitedCommunityCardView = Instantiate(categoryButtonPrefab, categoriesContainer);
            return invitedCommunityCardView;
        }

        private void CreateAndSetupCategoryButton(PlaceCategoriesSO.PlaceCategoryData categoryData)
        {
            PlaceCategoryButton categoryButtonView = categoryButtonsPool.Get();

            // Setup card data
            categoryButtonView.Configure(categoryData);

            // Setup card events
            categoryButtonView.buttonView.Button.onClick.RemoveAllListeners();
            categoryButtonView.buttonView.Button.onClick.AddListener(() => SelectCategory(categoryData.id));

            currentCategories.Add(new KeyValuePair<string, PlaceCategoryButton>(categoryData.id, categoryButtonView));
        }

        private void SelectCategory(string categoryId, bool invokeEvent = true)
        {
            foreach (var category in currentCategories)
                category.Value.buttonView.SetSelected(category.Key == categoryId);

            if (invokeEvent)
                CategorySelected?.Invoke(categoryId != ALL_CATEGORY_ID ? categoryId : null);
        }
    }
}
