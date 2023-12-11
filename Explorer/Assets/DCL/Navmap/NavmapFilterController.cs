
using System;

namespace DCL.Navmap
{
    public class NavmapFilterController
    {
        private readonly NavmapFilterView filterView;

        public NavmapFilterController(NavmapFilterView filterView)
        {
            this.filterView = filterView;
            filterView.filterButton.onClick.AddListener(ToggleFilterContent);
            filterView.infoButton.onClick.AddListener(ToggleInfoContent);
        }

        private void ToggleInfoContent()
        {
            filterView.infoContent.SetActive(!filterView.infoContent.activeSelf);
        }

        private void ToggleFilterContent()
        {
            //Add animations
            filterView.filterContentTransform.gameObject.SetActive(!filterView.filterContentTransform.gameObject.activeSelf);
            filterView.infoContent.SetActive(false);
        }
    }
}
