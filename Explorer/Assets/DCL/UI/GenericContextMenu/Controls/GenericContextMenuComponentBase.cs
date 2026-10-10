using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI.Controls
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(HorizontalLayoutGroup))]
    public abstract class GenericContextMenuComponentBase : MonoBehaviour, IPointerDownHandler
    {
        public event Action<GenericContextMenuComponentBase> OnPointerDownEvent;

        [field: SerializeField] public RectTransform RectTransformComponent { get; private set; }
        [field: SerializeField] public HorizontalLayoutGroup HorizontalLayoutComponent { get; private set; }

        public abstract bool IsInteractable { get; set; }

        public abstract void UnregisterListeners();

        public abstract void RegisterCloseListener(Action listener);

        public string NonInteractableFeedback { get; set; }

        public void OnPointerDown(PointerEventData _) =>
            OnPointerDownEvent?.Invoke(this);
    }
}
