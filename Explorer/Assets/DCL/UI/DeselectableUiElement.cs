using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class DeselectableUiElement : MonoBehaviour, IDeselectHandler
{
    public event Action OnDeselectEvent;

    public void SelectElement() => EventSystem.current.SetSelectedGameObject(gameObject);

    public void OnDeselect(BaseEventData eventData) =>
        OnDeselectEvent?.Invoke();
}
