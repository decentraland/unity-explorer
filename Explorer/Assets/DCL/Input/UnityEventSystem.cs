using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace DCL.Input
{
    public class UnityEventSystem : IEventSystem
    {
        private readonly EventSystem eventSystem;

        public UnityEventSystem(EventSystem eventSystem)
        {
            this.eventSystem = eventSystem;
        }

        public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults)
        {
            eventSystem.RaycastAll(eventData, raycastResults);
        }

        public PointerEventData GetPointerEventData() =>
            new (eventSystem);
    }

    public interface IEventSystem
    {
        void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults);

        PointerEventData GetPointerEventData();
    }
}
