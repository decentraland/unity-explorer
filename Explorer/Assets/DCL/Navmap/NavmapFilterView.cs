using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class NavmapFilterView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform filterContentTransform;

        [field: SerializeField]
        public Button filterButton;

        [field: SerializeField]
        public Button infoButton;

        [field: SerializeField]
        public GameObject infoContent;
    }
}
