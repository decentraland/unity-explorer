using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Minimap
{
    public class SideMenuView : MonoBehaviour
    {
        [field: SerializeField]
        public ToggleView ToggleHome { get; private set; }

        [field: SerializeField]
        public ToggleView ToggleSceneUI { get; private set; }

        [field: SerializeField]
        public Button TwitterButton { get; private set; }

        [field: SerializeField]
        public Button CopyCoordinatesButton { get; private set; }

        [field: SerializeField]
        public Button CopyLinkButton { get; private set; }

        [field: SerializeField]
        public Button InfoButton { get; private set; }
    }
}
