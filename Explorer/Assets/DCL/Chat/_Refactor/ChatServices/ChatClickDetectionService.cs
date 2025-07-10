// In Services/ChatClickDetectionService.cs

using System;
using System.Collections.Generic;
using DCL.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; // Make sure this is included

public class ChatClickDetectionService : IDisposable
{
    public event Action OnClickInside;
    public event Action OnClickOutside;

    private readonly RectTransform targetArea;
    private readonly DCLInput dclInput;

    public ChatClickDetectionService(RectTransform targetArea)
    {
        this.targetArea = targetArea;
        this.dclInput = DCLInput.Instance;
    }

    public void Initialize()
    {
        dclInput.UI.Click.performed += HandleGlobalClick;
    }

    public void Dispose()
    {
        if (dclInput != null)
            dclInput.UI.Click.performed -= HandleGlobalClick;
    }

    private void HandleGlobalClick(InputAction.CallbackContext context)
    {
        if (EventSystem.current == null) return;

        var eventData = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        bool clickedInside = false;
        foreach (var result in results)
        {
            if (result.gameObject.transform.IsChildOf(targetArea))
            {
                clickedInside = true;
                break;
            }
        }

        if (clickedInside)
        {
            OnClickInside?.Invoke();
        }
        else
        {
            OnClickOutside?.Invoke();
        }
    }
}