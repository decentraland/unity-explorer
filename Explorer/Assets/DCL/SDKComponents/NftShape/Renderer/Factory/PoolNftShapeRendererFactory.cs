using DCL.Optimization.Pools;
using DCL.SDKComponents.NftShape.Frame;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Renderer.Factory
{
    /// <summary>
    /// Is not thread safe
    /// </summary>
    public class PoolNftShapeRendererFactory : INftShapeRendererFactory
    {
        private readonly IComponentPool<INftShapeRenderer> componentPool;

        private Transform tempTransform = null!;

        public PoolNftShapeRendererFactory(IComponentPoolsRegistry componentPoolsRegistry, IFramesPool framesPool) : this(new NftShapeRendererFactory(framesPool), componentPoolsRegistry) { }

        public PoolNftShapeRendererFactory(INftShapeRendererFactory origin, IComponentPoolsRegistry componentPoolsRegistry)
        {
            componentPool = new ThreadSafeComponentPool<INftShapeRenderer>(() => origin.New(tempTransform));
            componentPoolsRegistry.AddComponentPool(componentPool);
        }

        public INftShapeRenderer New(Transform parent)
        {
            tempTransform = parent;
            return componentPool.Get()!;
        }
    }
}
