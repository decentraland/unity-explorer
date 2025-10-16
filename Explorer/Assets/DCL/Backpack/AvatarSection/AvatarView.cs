using System;
using DCL.Backpack.AvatarSection.Outfits;
using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class AvatarView : MonoBehaviour
    {
        public Material blitMaterial; 
        
        [field: Header("Tabs")]
        [field: SerializeField]
        public AvatarPanelTabSelectorMapping[] TabSelectorMappedViews { get; private set; }
        
        [field: SerializeField]
        public TabSelectorView CategoriesTabSelector { get; private set; }

        [field: SerializeField]
        public TabSelectorView OutfitsTabSelector { get; private set; }

        [field: Header("Sub-Views")]
        [field: SerializeField]
        public CategoriesView CategoriesView { get; private set; }

        [field: SerializeField]
        public OutfitsView OutfitsView { get; private set; }

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

        [field: SerializeField]
        public Button marketplaceButton { get; private set; }
        
        
    }

    [Serializable]
    public struct AvatarPanelTabSelectorMapping
    {
        [field: SerializeField]
        public TabSelectorView TabSelectorView { get; private set; }

        [field: SerializeField]
        public AvatarSubSection Section { get; private set; }
    }
}
