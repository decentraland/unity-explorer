using Arch.Core;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Classes
{
    public class DCLTransform
    {
        public VisualElement VisualElement;
        public EntityReference Parent;
        public HashSet<EntityReference> Children;
        public bool IsHidden;
        public int RightOf;

        private EventCallback<PointerDownEvent> currentOnPointerDownCallback;
        private EventCallback<PointerUpEvent> currentOnPointerUpCallback;

        public void RegisterPointerDownCallback(EventCallback<PointerDownEvent> newOnPointerDownCallback)
        {
            if (currentOnPointerDownCallback != null)
                VisualElement.UnregisterCallback(currentOnPointerDownCallback);

            VisualElement.RegisterCallback(newOnPointerDownCallback);
            currentOnPointerDownCallback = newOnPointerDownCallback;
        }

        public void RegisterPointerUpCallback(EventCallback<PointerUpEvent> newOnPointerUpCallback)
        {
            if (currentOnPointerUpCallback != null)
                VisualElement.UnregisterCallback(currentOnPointerUpCallback);

            VisualElement.RegisterCallback(newOnPointerUpCallback);
            currentOnPointerUpCallback = newOnPointerUpCallback;
        }

        public void UnregisterAllCallbacks()
        {
            if (currentOnPointerDownCallback != null)
                VisualElement.UnregisterCallback(currentOnPointerDownCallback);

            if (currentOnPointerUpCallback != null)
                VisualElement.UnregisterCallback(currentOnPointerUpCallback);
        }

        public void Dispose()
        {
            UnregisterAllCallbacks();
        }
    }
}
