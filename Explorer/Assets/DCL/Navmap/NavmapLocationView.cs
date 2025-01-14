using DCL.Navmap.FilterPanel;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class NavmapLocationView : MonoBehaviour
    {
        [field: SerializeField]
        public Button FilterPanelButton { get; private set; }

        [field: SerializeField]
        public Button CenterToPlayerButton { get; private set; }

        [field: SerializeField]
        public Button CenterToHomeButton { get; private set; }

        [field: SerializeField]
        public NavmapFilterPanelView FiltersPanel { get; private set; }

    }
}
