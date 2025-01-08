using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(HorizontalLayoutGroup))]
    public abstract class GenericContextMenuComponent : MonoBehaviour, IGenericContextMenuComponent
    {
        [field: SerializeField] public RectTransform RectTransformComponent { get; private set; }
        [field: SerializeField] public HorizontalLayoutGroup HorizontalLayoutComponent { get; private set; }

        public abstract void Configure(ContextMenuControlSettings settings, object initialValue);

        public abstract void UnregisterListeners();

        public abstract void RegisterListener(Delegate listener);

        public abstract void RegisterCloseListener(Action listener);
    }
}
