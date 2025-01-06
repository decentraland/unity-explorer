using System;
using UnityEngine;
using Utility;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    [Serializable]
    public class ContextMenuControlSettings : ScriptableObject
    {
        [SerializeField, ReadOnly] protected ContextMenuControlTypes controlTypeType;

        public ContextMenuControlTypes ControlTypeType => controlTypeType;
    }
}
