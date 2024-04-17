using DCL.CharacterPreview;
using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class BackpackView : MonoBehaviour
    {
        [field: SerializeField]
        public BackpackPanelTabSelectorMapping[] TabSelectorMappedViews { get; private set; }

        [field: SerializeField]
        public BackpackSortDropdownView BackpackSortView { get; private set; }

        [field: SerializeField]
        public Button TipsButton { get; private set; }

        [field: SerializeField]
        public DeselectableUiElement TipsPanelDeselectable { get; private set; }

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; }
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
