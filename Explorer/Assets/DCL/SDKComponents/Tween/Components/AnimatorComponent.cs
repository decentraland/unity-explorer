using DCL.Optimization.Pools;
using System;

namespace DCL.SDKComponents.Tween.Components
{
    public class AnimatorComponent : IPoolableComponentProvider<SDKAnimatorComponent>
    {
        public SDKAnimatorComponent SDKAnimatorComponent;

        public void Dispose()
        {
        }

        SDKAnimatorComponent IPoolableComponentProvider<SDKAnimatorComponent>.PoolableComponent => SDKAnimatorComponent;
        Type IPoolableComponentProvider<SDKAnimatorComponent>.PoolableComponentType  => typeof(SDKTweenComponent);
    }
}
