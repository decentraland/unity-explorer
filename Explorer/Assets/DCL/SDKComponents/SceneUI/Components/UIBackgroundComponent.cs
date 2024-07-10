using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using JetBrains.Annotations;
using System;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.SceneUI.Components
{
    public struct UIBackgroundComponent : IPoolableComponentProvider<DCLImage>
    {
        [CanBeNull] public DCLImage Image;
        public Promise? TexturePromise;
        public ECS.StreamableLoading.LifeCycle Status;

        DCLImage IPoolableComponentProvider<DCLImage>.PoolableComponent => Image;
        Type IPoolableComponentProvider<DCLImage>.PoolableComponentType => typeof(DCLImage);

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}
