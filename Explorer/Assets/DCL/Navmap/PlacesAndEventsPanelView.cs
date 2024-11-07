using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class PlacesAndEventsPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public Button ExpandButton { get; private set; }

        [field: SerializeField]
        public Button CollapseButton { get; private set; }

        [field: SerializeField]
        public PlaceInfoPanelView PlaceInfoPanelView { get; private set; }

        [field: SerializeField]
        public SearchFiltersView SearchFiltersView { get; private set; }
    }
}
