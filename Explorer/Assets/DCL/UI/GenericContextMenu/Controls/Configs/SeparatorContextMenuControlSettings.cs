using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/Components/SeparatorContextMenuControlSettings")]
    [Serializable]
    public class SeparatorContextMenuControlSettings : ContextMenuControlSettings
    {
        [SerializeField] private int height;

        public int Height => height;

        private void OnEnable() =>
            controlTypeType = ContextMenuControlTypes.SEPARATOR;
    }
}
