using DCL.UI;
using UnityEngine;

namespace DCL.Backpack
{
    public class AvatarView : MonoBehaviour
    {
        [field: SerializeField]
        public BackpackGridView backpackGridView { get; private set; }

        [field: SerializeField]
        public BackpackInfoPanelView backpackInfoPanelView { get; private set; }

        [field: SerializeField]
        public SearchBarView backpackSearchBar { get; private set; }
    }
}
