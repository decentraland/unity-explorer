using DCL.UI.GenericContextMenu.Controls.Configs;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls
{
    [RequireComponent(typeof(RectTransform))]
    public abstract class GenericContextMenuComponent : MonoBehaviour, IGenericContextMenuComponent
    {
        [field: SerializeField] public RectTransform RectTransformComponent { get; private set; }

        public abstract void Configure(ContextMenuControlSettings settings);

        public abstract void UnregisterListeners();
    }
}
