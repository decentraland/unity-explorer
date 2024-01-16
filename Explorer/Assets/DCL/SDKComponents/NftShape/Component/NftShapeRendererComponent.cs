using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.NftShape.Renderer;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Component
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
        }

        public INftShapeRenderer PoolableComponent => nftShape;
    }
}
