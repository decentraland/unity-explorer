using System;

namespace DCL.Places
{
    public class DiscoverSectionController : IDisposable
    {
        private readonly DiscoverSectionView view;
        private readonly PlacesController placesController;

        private bool isSectionOpen = false;

        public DiscoverSectionController(
            PlacesController placesController,
            DiscoverSectionView view)
        {
            this.view = view;
            this.placesController = placesController;

            placesController.SectionChanged += OnSectionChanged;
            placesController.PlacesClosed += OnPlacesSectionClosed;
        }

        public void Dispose()
        {
            placesController.SectionChanged -= OnSectionChanged;
            UnloadSection();
        }

        private void OnSectionChanged(PlacesSections? fromSection, PlacesSections toSection)
        {
            if (toSection == PlacesSections.DISCOVER)
            {
                LoadSection();
                isSectionOpen = true;
            }
            else if (fromSection == PlacesSections.DISCOVER)
            {
                UnloadSection();
                isSectionOpen = false;
            }
        }

        private void OnPlacesSectionClosed()
        {
            if (!isSectionOpen)
                return;

            UnloadSection();
        }

        private void LoadSection()
        {
            view.SetCategories(new []{"all", "poi", "featured", "game"});
        }

        private void UnloadSection()
        {
            view.ClearCategories();
        }
    }
}
