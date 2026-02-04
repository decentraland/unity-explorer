using DCL.Browser;
using DCL.Communities;
using DCL.Friends;
using DCL.Input;
using DCL.Input.Component;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.PlacesAPIService;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using MVC;
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
            IInputBlock inputBlock,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IMVCManager mvcManager,
            ThumbnailLoader thumbnailLoader,
            PlacesCardSocialActionsController placesCardSocialActionsController,
            HomePlaceEventBus homePlaceEventBus)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.placesCategories = placesCategories;
            this.inputBlock = inputBlock;

            placesStateService = new PlacesStateService();
            placesResultsController = new PlacesResultsController(view.PlacesResultsView, this, placesAPIService, placesStateService, placesCategories, selfProfile, webBrowser,
                friendServiceProxy, profileRepositoryWrapper, mvcManager, thumbnailLoader, placesCardSocialActionsController, homePlaceEventBus);

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
            view.ResetFiltersDropdown();
            view.SetCategories(placesCategories.categories);
            view.OpenSection(PlacesSection.BROWSE, force: true);
            cursor.Unlock();
        }

        public void Deactivate()
        {
            // Must be before setting the view inactive or the focus state will be false regardless
            if (view.IsSearchBarFocused)
                RestoreShortcutsInput();

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

        public void OpenSection(PlacesSection section, bool force = false, bool invokeEvent = true, bool cleanSearch = true)
        {
            view.SetFiltersVisible(section == PlacesSection.BROWSE);
            view.OpenSection(section, force, invokeEvent, cleanSearch);
        }

        private void OnAnyFilterChanged(PlacesFilters newFilters)
        {
            view.SetFiltersVisible(newFilters.Section == PlacesSection.BROWSE);
            view.SetCategoriesVisible(newFilters.Section == PlacesSection.BROWSE && string.IsNullOrEmpty(newFilters.SearchText));
            FiltersChanged?.Invoke(newFilters);
        }

        private void DisableShortcutsInput() =>
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void RestoreShortcutsInput() =>
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);
    }
}
