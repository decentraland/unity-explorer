using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using System;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UIBackgroundComponent : IPoolableComponentProvider<DCLImage>
    {
        public DCLImage Image;

        DCLImage IPoolableComponentProvider<DCLImage>.PoolableComponent => Image;
        Type IPoolableComponentProvider<DCLImage>.PoolableComponentType => typeof(DCLImage);

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}
