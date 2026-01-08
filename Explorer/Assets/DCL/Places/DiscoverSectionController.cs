using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;

namespace DCL.Places
{
    public class DiscoverSectionController : IDisposable
    {
        private const string GET_CATEGORIES_ERROR_MESSAGE = "There was an error loading categories. Please try again.";

        private readonly DiscoverSectionView view;
        private readonly PlacesController placesController;
        private readonly IPlacesAPIService placesAPIService;

        private bool isSectionOpen;

        private CancellationTokenSource? getCategoriesCts;

        public DiscoverSectionController(
            PlacesController placesController,
            DiscoverSectionView view,
            IPlacesAPIService placesAPIService)
        {
            this.view = view;
            this.placesController = placesController;
            this.placesAPIService = placesAPIService;

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
            getCategoriesCts = getCategoriesCts.SafeRestart();
            LoadCategoriesAsync(getCategoriesCts.Token).Forget();
        }

        private void UnloadSection()
        {
            getCategoriesCts?.SafeCancelAndDispose();
            view.ClearCategories();
        }

        private async UniTask LoadCategoriesAsync(CancellationToken ct)
        {
            var categoriesResult = await placesAPIService.GetPlacesCategoriesAsync(ct)
                                                         .SuppressToResultAsync(ReportCategory.PLACES);

            if (!categoriesResult.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_CATEGORIES_ERROR_MESSAGE));
                return;
            }

            view.SetCategories(categoriesResult.Value.data);
        }
    }
}
