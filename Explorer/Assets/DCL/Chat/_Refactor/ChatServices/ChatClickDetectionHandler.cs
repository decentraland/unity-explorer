using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    /// Click detection service that uses centralized raycast results.
    /// </summary>
    public class ChatClickDetectionHandler : IDisposable
    {
        public event Action? OnClickInside;
        public event Action? OnClickOutside;

        private readonly RectTransform targetArea;
        private readonly HashSet<Transform> ignoredElementsSet;
        private readonly CentralizedChatClickDetectionService centralizedChatService;

        private bool isPaused;

        public ChatClickDetectionHandler(
            Transform targetArea,
            params Transform[] ignoredElements)
        {
            this.centralizedChatService = CentralizedChatClickDetectionService.Instance;
            this.targetArea = (RectTransform)targetArea;
            ignoredElementsSet = new HashSet<Transform>(ignoredElements);

            centralizedChatService.OnClickDetected += HandleClickDetected;
        }

        public void Dispose()
        {
            centralizedChatService.OnClickDetected -= HandleClickDetected;
            OnClickInside = null;
            OnClickOutside = null;
        }


        public void Pause()
        {
            if (!isPaused)
                centralizedChatService.OnClickDetected -= HandleClickDetected;
        }


        public void Resume()
        {
            if (isPaused)
                centralizedChatService.OnClickDetected += HandleClickDetected;
        }

        private void HandleClickDetected(RaycastResult? raycastResult)
        {
            if (isPaused) return;

            if (raycastResult == null)
            {
                OnClickOutside?.Invoke();
                return;
            }

            if (IsIgnored(raycastResult.Value.gameObject))
                return;

            bool clickedInside = raycastResult.Value.gameObject.transform.IsChildOf(targetArea);

            if (clickedInside)
                OnClickInside?.Invoke();
            else
                OnClickOutside?.Invoke();
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
}
