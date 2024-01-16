using DCL.CharacterPreview;
using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Backpack
{
    public class BackpackView : MonoBehaviour
    {
        [field: SerializeField]
        public BackpackPanelTabSelectorMapping[] TabSelectorMappedViews { get; private set; }

        [field: SerializeField]
        public BackpackSortDropdownView BackpackSortView { get; private set; }

        [field: SerializeField]
        internal BackpackCharacterPreviewView backpackCharacterPreviewView { get; private set; }
    }

    [Serializable]
    public struct BackpackPanelTabSelectorMapping
    {
        [field: SerializeField]
        public TabSelectorView TabSelectorViews { get; private set; }

        [field: SerializeField]
        public BackpackSections Section { get; private set; }
    }
}
