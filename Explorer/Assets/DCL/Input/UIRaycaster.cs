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
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            return raycastResults;
        }
    }

    public interface IUIRaycaster
    {
        IReadOnlyList<RaycastResult> RaycastAll(Vector2 position);
    }
}
