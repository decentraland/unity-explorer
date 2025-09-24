using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Controls
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(HorizontalLayoutGroup))]
    public abstract class GenericContextMenuComponentBase : MonoBehaviour
    {
        [field: SerializeField] public RectTransform RectTransformComponent { get; private set; }
        [field: SerializeField] public HorizontalLayoutGroup HorizontalLayoutComponent { get; private set; }

        public abstract void UnregisterListeners();

        public abstract void RegisterCloseListener(Action listener);
    }
}
