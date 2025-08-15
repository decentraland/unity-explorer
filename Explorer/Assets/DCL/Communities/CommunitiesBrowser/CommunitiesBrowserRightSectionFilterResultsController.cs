using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserRightSectionFilterResultsController : IDisposable
    {




        private readonly CommunitiesBrowserRightSectionFilterResultsView view;



        public CommunitiesBrowserRightSectionFilterResultsController(
            CommunitiesBrowserRightSectionFilterResultsView view)
        {
            this.view = view;
            view.ScrollChanged += LoadMoreResults; //SAME NEEDS TO HAPPEN ON THE BROWSE_ALL_SECTION

        }

        private void LoadMoreResults(Vector2 _)
        {
            if (isGridResultsLoadingItems ||
                browserOrchestrator.GetCommunitiesCount() >= currentResultsTotalAmount ||
                !view.IsResultsScrollPositionAtBottom)
                return;

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: currentNameFilter,
                currentOnlyMemberOf,
                pageNumber: currentPageNumberFilter + 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();
        }

        public void Dispose()
        { }
    }
}
