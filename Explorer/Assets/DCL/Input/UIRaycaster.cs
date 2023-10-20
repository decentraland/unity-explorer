using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Input
{
    public class UIRaycaster : IUIRaycaster
    {
        private readonly EventSystem eventSystem;
        private readonly PointerEventData pointerEventData;
        private readonly List<RaycastResult> raycastResults;

        public UIRaycaster(EventSystem eventSystem)
        {
            this.eventSystem = eventSystem;
            pointerEventData = new PointerEventData(eventSystem);
            raycastResults = new List<RaycastResult>();
        }

        public IReadOnlyList<RaycastResult> RaycastAll(Vector2 position)
        {
            pointerEventData.position = position;
            raycastResults.Clear();

            bool isInSafeAreaWidth = position.x < 15 || position.x > Screen.width - 15;
            bool isInSafeAreaHeight = position.y < 15 || position.y > Screen.height - 15;

            if (isInSafeAreaWidth || isInSafeAreaHeight)
            {
                raycastResults.Add(new RaycastResult());
                return raycastResults;
            }

            eventSystem.RaycastAll(pointerEventData, raycastResults);
            return raycastResults;
        }
    }

    public interface IUIRaycaster
    {
        IReadOnlyList<RaycastResult> RaycastAll(Vector2 position);
    }
}
