using Arch.Core;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Utils;
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

        internal EventCallback<PointerDownEvent> currentOnPointerDownCallback;
        internal EventCallback<PointerUpEvent> currentOnPointerUpCallback;

        public bool HasAnyPointerDownCallback => currentOnPointerDownCallback != null;
        public bool HasAnyPointerUpCallback => currentOnPointerUpCallback != null;

        public void Dispose()
        {
            HashSetPool<EntityReference>.Release(Children);
            this.UnregisterAllCallbacks();
        }
    }
}
