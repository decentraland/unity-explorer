using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls
{
    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/GenericContextMenuSettings")]
    [Serializable]
    public class GenericContextMenuConfig : ScriptableObject
    {
        public List<ContextMenuControlSettings> ContextMenuSettings { get; private set; } = new ();
        public Vector2 OffsetFromTarget { get; private set; } = Vector2.zero;
        public float Width { get; private set; } = 170;
    }

    [Serializable]
    public class ContextMenuControlSettings : ScriptableObject
    {
        public ContextMenuControlTypes ControlTypeType { get; private set; }
    }

    [Serializable]
    public class ButtonContextMenuControlSettings : ContextMenuControlSettings
    {
        public string ButtonText { get; private set; }
        public Sprite ButtonIcon { get; private set; }
    }

    [Serializable]
    public class ToggleContextMenuControlSettings : ContextMenuControlSettings
    {
        public string ToggleText { get; private set; }
    }

    [Serializable]
    public class SeparatorContextMenuControlSettings : ContextMenuControlSettings
    {
        public int Height { get; private set; }
    }
}
