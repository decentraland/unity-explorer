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

        [field: SerializeField]
        public Animator PanelAnimator { get; private set; }

        [field: SerializeField]
        public Animator HeaderAnimator { get; private set; }

        [field: Header("Compact layout")]
        [field: SerializeField]
        public RectTransform ContentRect { get; private set; }

        [field: SerializeField]
        public BackpackInfoPanelView[] ItemInfoPanels { get; private set; }

        /// <summary>
        ///     Rects centred on <see cref="ContentRect" />: trimming it moves them, so the compact layout puts them back.
        /// </summary>
        [field: SerializeField]
        public RectTransform[] CompactShiftedRects { get; private set; }

        /// <summary>
        ///     Outfits row, one fixed width strip that cannot reflow into the trimmed content.
        /// </summary>
        [field: SerializeField]
        public RectTransform OutfitsRect { get; private set; }

        /// <summary>
        ///     Closes the panel from inside it. Only the compact layout shows it: hosted full screen the panel is a section of
        ///     another panel, which brings its own close control.
        /// </summary>
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        /// <summary>
        ///     Search strip in the header. It spans the close button's slot as well while the button is hidden.
        /// </summary>
        [field: SerializeField]
        public RectTransform SearchBarRect { get; private set; }

        private void OnEnable()
        {
            PanelAnimator.enabled = true;
            HeaderAnimator.enabled = true;
        }

        private void OnDisable()
        {
            PanelAnimator.enabled = false;
            HeaderAnimator.enabled = false;
        }
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
