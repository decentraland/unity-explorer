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
        public VisualElement Transform;
        public EntityReference Parent;
        public HashSet<EntityReference> Children;
        public bool IsHidden;
        public int RightOf;

        UITransformComponent IPoolableComponentProvider<UITransformComponent>.PoolableComponent => this;
        Type IPoolableComponentProvider<UITransformComponent>.PoolableComponentType => typeof(UITransformComponent);

        private EventCallback<PointerDownEvent> currentOnPointerDownCallback;
        private EventCallback<PointerUpEvent> currentOnPointerUpCallback;

        public bool HasAnyPointerDownCallback => currentOnPointerDownCallback != null;
        public bool HasAnyPointerUpCallback => currentOnPointerUpCallback != null;

        public void RegisterPointerDownCallback(EventCallback<PointerDownEvent> newOnPointerDownCallback)
        {
            if (HasAnyPointerDownCallback)
                Transform.UnregisterCallback(currentOnPointerDownCallback);

            Transform.RegisterCallback(newOnPointerDownCallback);
            currentOnPointerDownCallback = newOnPointerDownCallback;
        }

        public void RegisterPointerUpCallback(EventCallback<PointerUpEvent> newOnPointerUpCallback)
        {
            if (HasAnyPointerUpCallback)
                Transform.UnregisterCallback(currentOnPointerUpCallback);

            Transform.RegisterCallback(newOnPointerUpCallback);
            currentOnPointerUpCallback = newOnPointerUpCallback;
        }

        public void UnregisterAllCallbacks()
        {
            if (HasAnyPointerDownCallback)
                Transform.UnregisterCallback(currentOnPointerDownCallback);

            if (HasAnyPointerUpCallback)
                Transform.UnregisterCallback(currentOnPointerUpCallback);
        }

        public void Dispose()
        {
            HashSetPool<EntityReference>.Release(Children);
            UnregisterAllCallbacks();
        }
    }
}
