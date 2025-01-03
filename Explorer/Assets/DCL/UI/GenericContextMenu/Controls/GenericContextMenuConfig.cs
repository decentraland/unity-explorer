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
        [SerializeField] private Vector2 offsetFromTarget = Vector2.zero;
        [SerializeField] private float width = 170;

        public List<ContextMenuControlSettings> ContextMenuSettings => contextMenuSettings;
        public Vector2 OffsetFromTarget => offsetFromTarget;
        public float Width => width;
    }

    [Serializable]
    public class ContextMenuControlSettings : ScriptableObject
    {
        [SerializeField] protected ContextMenuControlTypes controlTypeType;

        public ContextMenuControlTypes ControlTypeType => controlTypeType;
    }

    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/Components/ButtonContextMenuControlSettings")]
    [Serializable]
    public class ButtonContextMenuControlSettings : ContextMenuControlSettings
    {
        [SerializeField] private string buttonText;
        [SerializeField] private Sprite buttonIcon;

        public string ButtonText => buttonText;
        public Sprite ButtonIcon => buttonIcon;

        private void OnEnable() =>
            controlTypeType = ContextMenuControlTypes.BUTTON_WITH_TEXT_AND_ICON;
    }

    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/Components/ToggleContextMenuControlSettings")]
    [Serializable]
    public class ToggleContextMenuControlSettings : ContextMenuControlSettings
    {
        [SerializeField] private string toggleText;

        public string ToggleText => toggleText;

        private void OnEnable() =>
            controlTypeType = ContextMenuControlTypes.TOGGLE_WITH_TEXT;
    }

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
