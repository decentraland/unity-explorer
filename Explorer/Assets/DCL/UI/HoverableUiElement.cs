using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverableUiElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public event Action<bool>? HoverStateChanged;

    public void OnPointerEnter(PointerEventData eventData) =>
        HoverStateChanged?.Invoke(true);

    public void OnPointerExit(PointerEventData eventData) =>
        HoverStateChanged?.Invoke(false);
}
