using Arch.Core;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UITransformComponent : IPoolableComponentProvider<UITransformComponent>
    {
        UITransformComponent IPoolableComponentProvider<UITransformComponent>.PoolableComponent => this;
        Type IPoolableComponentProvider<UITransformComponent>.PoolableComponentType => typeof(UITransformComponent);

        public VisualElement VisualElement;
        public EntityReference Parent;
        public HashSet<EntityReference> Children;
        public bool IsHidden;
        public int RightOf;

        private EventCallback<PointerDownEvent> currentOnPointerDownCallback;
        private EventCallback<PointerUpEvent> currentOnPointerUpCallback;

        public bool HasAnyPointerDownCallback => currentOnPointerDownCallback != null;
        public bool HasAnyPointerUpCallback => currentOnPointerUpCallback != null;

        public void RegisterPointerDownCallback(EventCallback<PointerDownEvent> newOnPointerDownCallback)
        {
            if (HasAnyPointerDownCallback)
                VisualElement.UnregisterCallback(currentOnPointerDownCallback);

            VisualElement.RegisterCallback(newOnPointerDownCallback);
            currentOnPointerDownCallback = newOnPointerDownCallback;
        }

        public void RegisterPointerUpCallback(EventCallback<PointerUpEvent> newOnPointerUpCallback)
        {
            if (HasAnyPointerUpCallback)
                VisualElement.UnregisterCallback(currentOnPointerUpCallback);

            VisualElement.RegisterCallback(newOnPointerUpCallback);
            currentOnPointerUpCallback = newOnPointerUpCallback;
        }

        public void UnregisterAllCallbacks()
        {
            if (HasAnyPointerDownCallback)
                VisualElement.UnregisterCallback(currentOnPointerDownCallback);

            if (HasAnyPointerUpCallback)
                VisualElement.UnregisterCallback(currentOnPointerUpCallback);
        }

        public void Dispose()
        {
            HashSetPool<EntityReference>.Release(Children);
            UnregisterAllCallbacks();
        }
    }
}
