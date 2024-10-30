using System.Collections.Generic;

namespace DCL.Navmap
{
    public class NavmapSidePanelController
    {
        private readonly NavmapSearchBarController searchBarController;
        private readonly SearchResultPanelController searchResultController;
        private readonly EventInfoCardController eventInfoController;

        public NavmapSidePanelController(NavmapSearchBarController searchBarController,
            SearchResultPanelController searchResultController,
            EventInfoCardController eventInfoController)
        {
            this.searchBarController = searchBarController;
            this.searchResultController = searchResultController;
            this.eventInfoController = eventInfoController;
        }
    }
}
