using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

// Make sure this is included

namespace DCL.Chat.ChatServices
{
    public class ChatClickDetectionService : IDisposable
    {
        public event Action? OnClickInside;
        public event Action? OnClickOutside;

        private readonly Transform targetArea;
        private readonly HashSet<Transform> ignoredElementsSet;

        private bool isPaused;

        public ChatClickDetectionService(Transform targetArea, params Transform[] ignoredElements)
        {
            this.targetArea = targetArea;
            ignoredElementsSet = new HashSet<Transform>(ignoredElements);

            DCLInput.Instance.UI.Click.performed += HandleGlobalClick;
        }

        public void Dispose()
        {
            DCLInput.Instance.UI.Click.performed -= HandleGlobalClick;
        }

        public void Pause() =>
            isPaused = true;

        public void Resume() =>
            isPaused = false;

        private void HandleGlobalClick(InputAction.CallbackContext context)
        {
            if (EventSystem.current == null) return;
            if (isPaused) return;

            // var eventData = new PointerEventData(EventSystem.current)
            // {
            //     position = Mouse.current.position.ReadValue()
            // };

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = GetPointerPosition(context)
            };
        
            using PooledObject<List<RaycastResult>> _ = ListPool<RaycastResult>.Get(out List<RaycastResult>? results);
        
            EventSystem.current.RaycastAll(eventData, results);
        
            if (results.Count > 0 && IsIgnored(results[0].gameObject))
                return;
        
            var clickedInside = false;
        
            foreach (RaycastResult result in results)
            {
                if (result.gameObject.transform.IsChildOf(targetArea))
                {
                    clickedInside = true;
                    break;
                }
            }
        
            if (clickedInside) OnClickInside?.Invoke();
            else OnClickOutside?.Invoke();
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

        private static Vector2 GetPointerPosition(InputAction.CallbackContext ctx)
        {
            if (ctx.control is Pointer pCtrl) return pCtrl.position.ReadValue();
            if (Pointer.current != null) return Pointer.current.position.ReadValue();
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Touchscreen.current?.primaryTouch != null)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            return Vector2.zero;
        }
    }
}
