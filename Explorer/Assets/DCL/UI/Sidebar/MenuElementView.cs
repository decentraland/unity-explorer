using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.Sidebar
{
    /// <summary>
    ///     A single row of a sidebar menu. The owning menu decides which row is highlighted, so the row only
    ///     reports the pointer and exposes its highlight.
    /// </summary>
    public class MenuElementView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField] public ButtonView Button { get; private set; } = null!;
        [field: SerializeField] internal GameObject selectedObject { get; private set; } = null!;

        public event Action<MenuElementView>? PointerEntered;
        public event Action<MenuElementView>? PointerExited;

        public void SetSelected(bool selected)
        {
            selectedObject.SetActive(selected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEntered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExited?.Invoke(this);
        }
    }
}
