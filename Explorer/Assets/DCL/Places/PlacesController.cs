using DCL.Input;
using DCL.PlacesAPIService;
using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Places
{
    public class PlacesController : ISection, IDisposable
    {
        public event Action<PlacesFilters>? FiltersChanged;
        public event Action? PlacesClosed;

        private readonly PlacesView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;

        private bool isSectionActivated;
        private readonly PlacesStateService placesStateService;
        private readonly PlacesResultsController placesResultsController;
        private readonly PlaceCategoriesSO placesCategories;

        public PlacesController(
            PlacesView view,
            ICursor cursor,
            IPlacesAPIService placesAPIService,
            PlaceCategoriesSO placesCategories)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.placesCategories = placesCategories;

            placesStateService = new PlacesStateService();
            placesResultsController = new PlacesResultsController(view.PlacesResultsView, this, placesAPIService, placesStateService, placesCategories);

            view.SectionChanged += OnSectionChanged;
            view.CategorySelected += OnCategorySelected;
            view.SortByChanged += OnSortByChanged;
        }

        public void Dispose()
        {
            view.SectionChanged -= OnSectionChanged;
            view.CategorySelected -= OnCategorySelected;
            view.SortByChanged -= OnSortByChanged;

            placesStateService.Dispose();
            placesResultsController.Dispose();
        }

        public void Activate()
        {
            if (isSectionActivated)
                return;

            isSectionActivated = true;
            view.SetViewActive(true);
            view.ResetCurrentFilters();
            view.SetupSortByFilter();
            view.SetCategories(placesCategories.categories);
            view.OpenSection(PlacesSection.DISCOVER, true);
            cursor.Unlock();
        }

        public void Deactivate()
        {
            isSectionActivated = false;
            view.SetViewActive(false);
            view.ClearSortByFilter();
            view.ClearCategories();
            PlacesClosed?.Invoke();
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        private void OnSectionChanged(PlacesSection section)
        {
            view.SetCategoriesVisible(section == PlacesSection.DISCOVER);
            FiltersChanged?.Invoke(view.CurrentFilters);
        }

        private void OnCategorySelected(string? categoryId) =>
            FiltersChanged?.Invoke(view.CurrentFilters);

        private void OnSortByChanged(IPlacesAPIService.SortBy sortBy) =>
            FiltersChanged?.Invoke(view.CurrentFilters);
    }
}
