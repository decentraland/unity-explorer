using DCL.Input;
using DCL.Input.Component;
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
        private readonly IInputBlock inputBlock;

        public PlacesController(
            PlacesView view,
            ICursor cursor,
            IPlacesAPIService placesAPIService,
            PlaceCategoriesSO placesCategories,
            IInputBlock inputBlock)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.placesCategories = placesCategories;
            this.inputBlock = inputBlock;

            placesStateService = new PlacesStateService();
            placesResultsController = new PlacesResultsController(view.PlacesResultsView, this, placesAPIService, placesStateService, placesCategories);

            view.AnyFilterChanged += OnAnyFilterChanged;
            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreShortcutsInput;
        }

        public void Dispose()
        {
            view.AnyFilterChanged -= OnAnyFilterChanged;
            view.SearchBarSelected -= DisableShortcutsInput;
            view.SearchBarDeselected -= RestoreShortcutsInput;

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
            view.OpenSection(PlacesSection.DISCOVER, force: true);
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

        public void OpenSection(PlacesSection section, bool force = false, bool invokeEvent = true) =>
            view.OpenSection(section, force, invokeEvent);

        private void OnAnyFilterChanged(PlacesFilters newFilters)
        {
            view.SetCategoriesVisible(newFilters.Section == PlacesSection.DISCOVER && string.IsNullOrEmpty(newFilters.SearchText));
            FiltersChanged?.Invoke(newFilters);
        }

        private void DisableShortcutsInput() =>
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void RestoreShortcutsInput() =>
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);
    }
}
