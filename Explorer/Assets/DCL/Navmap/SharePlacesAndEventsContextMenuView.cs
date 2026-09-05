using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class SharePlacesAndEventsContextMenuView : MonoBehaviour
    {
        [field: SerializeField]
        public Button ShareOnTwitterButton { get; private set; }

        [field: SerializeField]
        public Button CopyLinkButton { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public GameObject MenuRoot { get; private set; }
    }
}
