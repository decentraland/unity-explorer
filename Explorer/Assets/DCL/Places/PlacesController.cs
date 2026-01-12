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

        private PlacesFilters currentFilters;

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
            placesResultsController = new PlacesResultsController(view.DiscoverView, this, placesAPIService, placesStateService);

            view.SectionChanged += OnSectionChanged;
            view.CategorySelected += OnCategorySelected;
        }

        public void Dispose()
        {

            view.SectionChanged -= OnSectionChanged;
            view.CategorySelected -= OnCategorySelected;

            placesStateService.Dispose();
            placesResultsController.Dispose();
        }

        public void Activate()
        {
            if (isSectionActivated)
                return;

            ResetCurrentFilters();
            isSectionActivated = true;
            view.SetViewActive(true);
            view.OpenSection(PlacesSection.DISCOVER, true);
            cursor.Unlock();
        }

        public void Deactivate()
        {
            isSectionActivated = false;
            view.SetViewActive(false);
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
            if (currentFilters.Section == section)
                return;

            view.SetCategoriesVisible(section == PlacesSection.DISCOVER);

            if (section == PlacesSection.DISCOVER)
                view.SetCategories(placesCategories.categories);
            else
                view.ClearCategories();

            currentFilters.Section = section;
            currentFilters.CategoryId = null;
            FiltersChanged?.Invoke(currentFilters);
        }

        private void OnCategorySelected(string? categoryId)
        {
            if (currentFilters.CategoryId == categoryId)
                return;

            currentFilters.CategoryId = categoryId;
            FiltersChanged?.Invoke(currentFilters);
        }

        private void ResetCurrentFilters()
        {
            currentFilters.Section = null;
            currentFilters.CategoryId = null;
        }
    }
}
