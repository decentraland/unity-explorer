using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; // Make sure this is included

public class ChatClickDetectionService : IDisposable
{
    public event Action OnClickInside;
    public event Action OnClickOutside;

    private RectTransform targetArea;
    private readonly DCLInput dclInput;
    private HashSet<Transform> ignoredElementsSet;
    private bool isPaused = false;
    public ChatClickDetectionService(RectTransform targetArea)
    {
        this.targetArea = targetArea;
        dclInput = DCLInput.Instance;
        ignoredElementsSet = new HashSet<Transform>();
    }

    public ChatClickDetectionService()
    {
        dclInput = DCLInput.Instance;
        ignoredElementsSet = new HashSet<Transform>();
    }

    public void Initialize(IReadOnlyList<Transform> elementsToIgnore)
    {
        ignoredElementsSet = new HashSet<Transform>(elementsToIgnore);
        if (dclInput != null)
            dclInput.UI.Click.performed -= HandleGlobalClick;
        
        if (dclInput != null) 
            dclInput.UI.Click.performed += HandleGlobalClick;
    }

    public void Initialize(RectTransform targetArea, IReadOnlyList<Transform> elementsToIgnore)
    {
        this.targetArea = targetArea;
        ignoredElementsSet = new HashSet<Transform>(elementsToIgnore);
        if (dclInput != null)
            dclInput.UI.Click.performed -= HandleGlobalClick;

        if (dclInput != null)
            dclInput.UI.Click.performed += HandleGlobalClick;
    }

    public void Pause() =>  isPaused = true;
    public void Resume() => isPaused = false;

    public void Dispose()
    {
        if (dclInput != null)
            dclInput.UI.Click.performed -= HandleGlobalClick;
    }

    private void HandleGlobalClick(InputAction.CallbackContext context)
    {
        if (EventSystem.current == null) return;
        if (isPaused) return;
        
        var eventData = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count > 0 && IsIgnored(results[0].gameObject))
            return;
        
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
    
    private bool IsIgnored(GameObject clickedObject)
    {
        if (clickedObject == null) return false;
        
        Transform current = clickedObject.transform;
        while (current != null)
        {
            if (ignoredElementsSet.Contains(current))
                return true;

            if (current == targetArea)
                return false;

            current = current.parent;
        }

        return false;
    }
}