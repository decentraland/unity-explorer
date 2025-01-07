using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/GenericContextMenuSettings")]
    [Serializable]
    public class GenericContextMenuConfig : ScriptableObject
    {
        [SerializeField] private List<ContextMenuControlSettings> contextMenuSettings = new ();
        [SerializeField] private Vector2 offsetFromTarget = Vector2.zero;
        [SerializeField] private float width = 170;
        [SerializeField] private RectOffset verticalLayoutPadding;
        [SerializeField] private int elementsSpacing = 1;

        public List<ContextMenuControlSettings> ContextMenuSettings => contextMenuSettings;
        public Vector2 OffsetFromTarget => offsetFromTarget;
        public float Width => width;
        public int ElementsSpacing => elementsSpacing;
        public RectOffset VerticalLayoutPadding => verticalLayoutPadding;
    }
}
