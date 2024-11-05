using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap.FilterPanel
{
    public class NavmapFilterPanelView : MonoBehaviour
    {
        [field: SerializeField]
        private Toggle minigamesToggle;

        [field: SerializeField]
        private Toggle liveEventsToggle;

        [field: SerializeField]
        private Toggle favoritesToggle;

        [field: SerializeField]
        private Toggle poisToggle;

        [field: SerializeField]
        private Toggle peopleToggle;

        [field: SerializeField]
        private Button satelliteButton;

        [field: SerializeField]
        private Button parcelButton;
    }
}
