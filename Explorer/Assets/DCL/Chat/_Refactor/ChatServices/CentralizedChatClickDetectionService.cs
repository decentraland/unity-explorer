using CodeLess.Attributes;
using DCL.Input.Utils;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    /// Centralized click detection service that performs a single raycast per click
    /// and distributes results to all subscribers.
    /// </summary>
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class CentralizedChatClickDetectionService : IDisposable
    {
        public event Action<RaycastResult?>? OnClickDetected;

        private bool isPaused;
        private static readonly ObjectPool<PointerEventData> POINTER_EVENT_DATA_POOL =
            new(() => new PointerEventData(EventSystem.current));

        public CentralizedChatClickDetectionService()
        {
            DCLInput.Instance.UI.Click.performed += HandleGlobalClick;
        }

        public void Dispose()
        {
            DCLInput.Instance.UI.Click.performed -= HandleGlobalClick;
            OnClickDetected = null;
            instance = null;
        }

        public void Pause() => isPaused = true;

        public void Resume() => isPaused = false;

        private void HandleGlobalClick(InputAction.CallbackContext context)
        {
            if (EventSystem.current == null) return;
            if (isPaused) return;
            if (OnClickDetected == null) return;

            var clickPosition = DCLInputUtilities.GetPointerPosition(context);

            using PooledObject<PointerEventData> pooledEventData = POINTER_EVENT_DATA_POOL.Get(out PointerEventData eventData);
            eventData.position = clickPosition;

            using PooledObject<List<RaycastResult>> pooledResults = ListPool<RaycastResult>.Get(out List<RaycastResult> results);
            EventSystem.current.RaycastAll(eventData, results);

            RaycastResult? result = results.Count > 0 ? results[0] : null;
            OnClickDetected?.Invoke(result);
        }
    }

}
