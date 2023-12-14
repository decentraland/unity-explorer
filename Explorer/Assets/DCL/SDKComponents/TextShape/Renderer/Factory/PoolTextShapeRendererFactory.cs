using DCL.Optimization.Pools;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Renderer.Factory
{
    /// <summary>
    /// Is not thread safe
    /// </summary>
    public class PoolTextShapeRendererFactory : ITextShapeRendererFactory
    {
        private readonly IComponentPool<ITextShapeRenderer> componentPool;

        private Transform tempTransform = null!;

        public PoolTextShapeRendererFactory(IComponentPoolsRegistry componentPoolsRegistry) : this(new TextShapeRendererFactory(), componentPoolsRegistry) { }

        public PoolTextShapeRendererFactory(ITextShapeRendererFactory origin, IComponentPoolsRegistry componentPoolsRegistry)
        {
            componentPool = new ThreadSafeComponentPool<ITextShapeRenderer>(() => origin.New(tempTransform));
            componentPoolsRegistry.AddComponentPool(componentPool);
        }

        public ITextShapeRenderer New(Transform parent)
        {
            tempTransform = parent;
            return componentPool.Get()!;
        }
    }
}
