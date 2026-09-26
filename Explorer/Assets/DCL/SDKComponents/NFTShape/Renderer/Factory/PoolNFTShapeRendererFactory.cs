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

        public PoolNFTShapeRendererFactory(IComponentPoolsRegistry componentPoolsRegistry, IFramesPool framesPool) : this(new NFTShapeRendererFactory(framesPool), componentPoolsRegistry) { }

        private PoolNFTShapeRendererFactory(INFTShapeRendererFactory origin, IComponentPoolsRegistry componentPoolsRegistry)
        {
            var poolRegistry = new GameObject(nameof(PoolNFTShapeRendererFactory));
            componentPool = new ComponentPool.WithFactory<INftShapeRenderer>(() => origin.New(poolRegistry.transform));
            componentPoolsRegistry.AddComponentPool(componentPool);
        }

        public INftShapeRenderer New(Transform parent)
        {
            var component = componentPool.Get()!;
            component.ApplyParent(parent);
            return component;
        }
    }
}
