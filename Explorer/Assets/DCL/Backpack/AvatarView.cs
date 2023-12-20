using UnityEngine;

namespace DCL.Backpack
{
    public class AvatarView : MonoBehaviour
    {
        [field: SerializeField]
        internal BackpackGridView backpackGridView { get; private set; }

        [field: SerializeField]
        internal BackpackInfoPanelView backpackInfoPanelView { get; private set; }
    }
}
