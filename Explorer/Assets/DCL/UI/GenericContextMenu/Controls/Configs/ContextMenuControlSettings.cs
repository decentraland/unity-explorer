using System;
using UnityEngine;
using Utility;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    [Serializable]
    public class ContextMenuControlSettings : ScriptableObject
    {
        [SerializeField, ShowOnly] protected ContextMenuControlTypes controlTypeType;

        public ContextMenuControlTypes ControlTypeType => controlTypeType;
    }
}
