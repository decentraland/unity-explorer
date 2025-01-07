using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/Components/ToggleContextMenuControlSettings")]
    [Serializable]
    public class ToggleContextMenuControlSettings : ContextMenuControlSettings
    {
        [SerializeField] private string toggleText;
        [SerializeField] private RectOffset horizontalLayoutPadding;
        [SerializeField] private int horizontalLayoutSpacing;
        [SerializeField] private bool horizontalLayoutReverseArrangement;

        public string ToggleText => toggleText;
        public RectOffset HorizontalLayoutPadding => horizontalLayoutPadding;
        public bool HorizontalLayoutReverseArrangement => horizontalLayoutReverseArrangement;
        public int HorizontalLayoutSpacing => horizontalLayoutSpacing;

        private void OnEnable() =>
            controlTypeType = ContextMenuControlTypes.TOGGLE_WITH_TEXT;
    }
}
