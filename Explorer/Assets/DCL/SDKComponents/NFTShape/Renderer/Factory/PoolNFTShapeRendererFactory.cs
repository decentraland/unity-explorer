using DCL.Optimization.Pools;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Renderer.Factory
{
    /// <summary>
    /// Is not thread safe
    /// </summary>
    public class PoolNFTShapeRendererFactory : INFTShapeRendererFactory
    {
        private readonly IComponentPool<INftShapeRenderer> componentPool;

        private Transform tempTransform = null!;

        public PoolNFTShapeRendererFactory(IComponentPoolsRegistry componentPoolsRegistry, IFramesPool framesPool) : this(new NFTShapeRendererFactory(framesPool), componentPoolsRegistry) { }

        private PoolNFTShapeRendererFactory(INFTShapeRendererFactory origin, IComponentPoolsRegistry componentPoolsRegistry)
        {
            componentPool = new ComponentPool.WithFactory<INftShapeRenderer>(() => origin.New(tempTransform));
            componentPoolsRegistry.AddComponentPool(componentPool);
        }

        public INftShapeRenderer New(Transform parent)
        {
            tempTransform = parent;
            return componentPool.Get()!;
        }
    }
}
