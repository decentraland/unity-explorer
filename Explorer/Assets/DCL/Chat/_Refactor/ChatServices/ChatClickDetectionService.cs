using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Chat.ChatServices
{
    public class ChatClickDetectionService : IDisposable
    {
        public event Action? OnClickInside;
        public event Action? OnClickOutside;

        private readonly RectTransform targetArea;
        private readonly HashSet<Transform> ignoredElementsSet;

        private bool isPaused;

        public ChatClickDetectionService(Transform targetArea, params Transform[] ignoredElements)
        {
            this.targetArea = (RectTransform)targetArea;
            ignoredElementsSet = new HashSet<Transform>(ignoredElements);
        }

        public void Pause() =>
            isPaused = true;

        public void Resume() =>
            isPaused = false;

        public void ProcessRaycastResults(IReadOnlyList<RaycastResult> results)
        {
            if (isPaused) return;

            if (results.Count == 0)
            {
                OnClickOutside?.Invoke();
                return;
            }

            if (IsIgnored(results[0].gameObject))
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

        public void Dispose()
        { }
    }
}
