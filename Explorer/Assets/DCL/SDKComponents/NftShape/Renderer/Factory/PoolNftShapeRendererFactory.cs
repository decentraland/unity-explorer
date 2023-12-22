using DCL.Optimization.Pools;
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

        public PoolNftShapeRendererFactory(IComponentPoolsRegistry componentPoolsRegistry) : this(new NftShapeRendererFactory(), componentPoolsRegistry) { }

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
