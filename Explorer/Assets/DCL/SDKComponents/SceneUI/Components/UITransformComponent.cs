using Arch.Core;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UITransformComponent : IPoolableComponentProvider<VisualElement>
    {
        public VisualElement Transform;
        public EntityReference Parent;
        public HashSet<EntityReference> Children;
        public bool IsHidden;
        public int RightOf;

        VisualElement IPoolableComponentProvider<VisualElement>.PoolableComponent => Transform;
        Type IPoolableComponentProvider<VisualElement>.PoolableComponentType => typeof(VisualElement);

        public void Dispose()
        {
            HashSetPool<EntityReference>.Release(Children);
        }
    }
}
