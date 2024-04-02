
using System;

namespace DCL.Navmap
{
    public class NavmapFilterController
    {
        private readonly NavmapFilterView filterView;

        public NavmapFilterController(NavmapFilterView filterView)
        {
            this.filterView = filterView;
            filterView.infoButton.onClick.AddListener(ToggleInfoContent);
        }

        private void ToggleInfoContent()
        {
            filterView.infoContent.SetActive(!filterView.infoContent.activeSelf);
        }
    }
}
