using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    [Serializable]
    public class ContextMenuControlSettings : ScriptableObject
    {
        [SerializeField] protected ContextMenuControlTypes controlTypeType;

        public ContextMenuControlTypes ControlTypeType => controlTypeType;
    }
}
