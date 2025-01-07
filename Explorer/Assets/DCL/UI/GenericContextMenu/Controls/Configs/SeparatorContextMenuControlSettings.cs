using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/Components/SeparatorContextMenuControlSettings")]
    [Serializable]
    public class SeparatorContextMenuControlSettings : ContextMenuControlSettings
    {
        [SerializeField] private int height;
        [SerializeField] private int leftPadding;
        [SerializeField] private int rightPadding;

        public int Height => height;
        public int LeftPadding => leftPadding;
        public int RightPadding => rightPadding;

        private void OnEnable() =>
            controlTypeType = ContextMenuControlTypes.SEPARATOR;
    }
}
