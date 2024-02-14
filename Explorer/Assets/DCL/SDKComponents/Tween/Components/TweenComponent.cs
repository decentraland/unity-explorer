using DCL.Optimization.Pools;
using DCL.SDKComponents.Tween.Components;
using System;

namespace DCL.SDKComponents.Tween.Components
{
    public class TweenComponent : IPoolableComponentProvider<SDKTweenComponent>
    {
        public SDKTweenComponent SDKTweenComponent;

        public void Dispose()
        {
        }

        SDKTweenComponent IPoolableComponentProvider<SDKTweenComponent>.PoolableComponent => SDKTweenComponent;
        Type IPoolableComponentProvider<SDKTweenComponent>.PoolableComponentType  => typeof(SDKTweenComponent);
    }
}
