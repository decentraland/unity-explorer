
namespace DCL.Navmap
{
    public class NavmapFilterController
    {
        private readonly NavmapFilterView filterView;

        public NavmapFilterController(NavmapFilterView filterView)
        {
            this.filterView = filterView;
            filterView.filterButton.onClick.RemoveAllListeners();
            filterView.filterButton.onClick.AddListener(ToggleFilterContent);
        }

        private void ToggleFilterContent()
        {
            //Add animations
            filterView.filterContentTransform.gameObject.SetActive(!filterView.filterContentTransform.gameObject.activeSelf);
        }
    }
}
