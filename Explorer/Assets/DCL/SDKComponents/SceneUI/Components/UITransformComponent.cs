using Arch.Core;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using System;
using UnityEngine.Pool;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UITransformComponent : IPoolableComponentProvider<DCLTransform>
    {
        public DCLTransform Transform;

        DCLTransform IPoolableComponentProvider<DCLTransform>.PoolableComponent => Transform;
        Type IPoolableComponentProvider<DCLTransform>.PoolableComponentType => typeof(DCLTransform);

        public void Dispose()
        {
            HashSetPool<EntityReference>.Release(Transform.Children);
            Transform.Dispose();
        }
    }
}
