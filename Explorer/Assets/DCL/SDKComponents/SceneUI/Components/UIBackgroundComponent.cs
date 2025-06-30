using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using ECS.StreamableLoading;
using System;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UIBackgroundComponent : IPoolableComponentProvider<DCLImage>
    {
        public DCLImage Image;
        public Promise? TexturePromise;
        public LifeCycle Status;

        DCLImage IPoolableComponentProvider<DCLImage>.PoolableComponent => Image;
        Type IPoolableComponentProvider<DCLImage>.PoolableComponentType => typeof(DCLImage);

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            //TODO do we need to adopt it to general budgeted finalize system?
            IsDisposed = true;
            Image = null;
        }
    }
}
