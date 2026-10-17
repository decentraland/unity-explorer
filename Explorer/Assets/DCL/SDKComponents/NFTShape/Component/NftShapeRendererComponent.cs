using DCL.Optimization.Pools;
using DCL.SDKComponents.NFTShape.Renderer;
using System;

namespace DCL.SDKComponents.NFTShape.Component
{
    public readonly struct NftShapeRendererComponent : IPoolableComponentProvider<INftShapeRenderer>
    {
        private readonly INftShapeRenderer nftShape;

        public NftShapeRendererComponent(INftShapeRenderer nftShape)
        {
            this.nftShape = nftShape;
        }

        public void ApplyVisibility(bool visibility)
        {
            if (visibility)
                nftShape.Show();
            else
                nftShape.Hide();
        }

        public void Dispose()
        {
            nftShape.Dispose();
        }

        public INftShapeRenderer PoolableComponent => nftShape;

        public Type PoolableComponentType => typeof(INftShapeRenderer);
    }
}
