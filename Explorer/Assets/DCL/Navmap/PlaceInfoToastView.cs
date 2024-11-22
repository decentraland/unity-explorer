using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class PlaceInfoToastView : MonoBehaviour
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public PlaceInfoPanelView PlacePanelView { get; private set; }
    }
}
