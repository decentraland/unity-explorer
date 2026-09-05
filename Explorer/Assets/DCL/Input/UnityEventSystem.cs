using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Input
{
    public class UnityEventSystem : IEventSystem
    {
        private const int SCREEN_SAFE_MARGIN = 15;
        private readonly EventSystem eventSystem;
        private readonly PointerEventData pointerEventData;
        private readonly List<RaycastResult> raycastResults;

        public UnityEventSystem(EventSystem eventSystem)
        {
            this.eventSystem = eventSystem;
            pointerEventData = new PointerEventData(eventSystem);
            raycastResults = new List<RaycastResult>();
        }

        public IReadOnlyList<RaycastResult> RaycastAll(Vector2 position)
        {
            pointerEventData.position = position;
            raycastResults.Clear();

            bool isInSafeAreaWidth = position.x < SCREEN_SAFE_MARGIN || position.x > Screen.width - SCREEN_SAFE_MARGIN;
            bool isInSafeAreaHeight = position.y < SCREEN_SAFE_MARGIN || position.y > Screen.height - SCREEN_SAFE_MARGIN;

            if (isInSafeAreaWidth || isInSafeAreaHeight)
            {
                raycastResults.Add(new RaycastResult());
                return raycastResults;
            }

            eventSystem.RaycastAll(pointerEventData, raycastResults);
            return raycastResults;
        }

        public bool IsPointerOverGameObject() =>
            eventSystem.IsPointerOverGameObject();
    }

    public interface IEventSystem
    {
        // Make sure that this is being called ONCE (disregard tests)
        IReadOnlyList<RaycastResult> RaycastAll(Vector2 position);

        bool IsPointerOverGameObject();
    }
}
