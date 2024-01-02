using DCL.Optimization.Pools;
using System;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UITransformComponent : IPoolableComponentProvider<VisualElement>
    {
        public VisualElement Transform;
        public VisualElement PoolableComponent => Transform;
        
        VisualElement IPoolableComponentProvider<VisualElement>.PoolableComponent => Transform;
        Type IPoolableComponentProvider<VisualElement>.PoolableComponentType => typeof(VisualElement);
        
        public void Dispose() { }
    }
}
