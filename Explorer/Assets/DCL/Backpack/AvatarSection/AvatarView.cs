using DCL.UI;
using UnityEngine;

namespace DCL.Backpack
{
    public class AvatarView : MonoBehaviour
    {
        [field: SerializeField]
        public TabSelectorView CategoriesTabSelector { get; private set; }

        [field: SerializeField]
        public TabSelectorView OutfitsTabSelector { get; private set; }

        [field: SerializeField]
        public GameObject CategoriesContainer { get; private set; }

        [field: SerializeField]
        public GameObject OutfitsContainer { get; private set; }
        
        [field: SerializeField]
        public BackpackGridView backpackGridView { get; private set; }

        [field: SerializeField]
        public BackpackInfoPanelView backpackInfoPanelView { get; private set; }

        [field: SerializeField]
        public SearchBarView backpackSearchBar { get; private set; }
    }
}
