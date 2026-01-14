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

            view.AnyFilterChanged += OnAnyFilterChanged;
        }

        public void Dispose()
        {
            view.AnyFilterChanged -= OnAnyFilterChanged;

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
            view.SetupSDKVersionFilter();
            view.SetCategories(placesCategories.categories);
            view.OpenSection(PlacesSection.DISCOVER, true);
            cursor.Unlock();
        }

        public void Deactivate()
        {
            isSectionActivated = false;
            view.SetViewActive(false);
            view.ClearSortByFilter();
            view.ClearSDKVersionFilter();
            view.ClearCategories();
            PlacesClosed?.Invoke();
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        private void OnAnyFilterChanged(PlacesFilters newFilters)
        {
            view.SetCategoriesVisible(newFilters.Section == PlacesSection.DISCOVER);
            FiltersChanged?.Invoke(newFilters);
        }
    }
}
