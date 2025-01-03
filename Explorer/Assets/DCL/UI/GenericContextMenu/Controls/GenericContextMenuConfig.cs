using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls
{
    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/GenericContextMenuSettings")]
    [Serializable]
    public class GenericContextMenuConfig : ScriptableObject
    {
        [SerializeField] private List<ContextMenuControlSettings> contextMenuSettings = new ();
    }

    [Serializable]
    public class ContextMenuControlSettings
    {
        public ContextMenuControlTypes controlTypeType;
        public string text;
        public Sprite icon;
    }
}
