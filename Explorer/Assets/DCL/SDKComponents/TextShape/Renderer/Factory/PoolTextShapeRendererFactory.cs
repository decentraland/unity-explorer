using DCL.Optimization.Pools;
using DCL.SDKComponents.TextShape.Fonts;
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

        public PoolTextShapeRendererFactory(IComponentPoolsRegistry componentPoolsRegistry, IFontsStorage fontsStorage) : this(new TextShapeRendererFactory(fontsStorage), componentPoolsRegistry) { }

        public PoolTextShapeRendererFactory(ITextShapeRendererFactory origin, IComponentPoolsRegistry componentPoolsRegistry)
        {
            componentPool = new ComponentPool.WithFactory<ITextShapeRenderer>(() => origin.New(tempTransform));
            componentPoolsRegistry.AddComponentPool(componentPool);
        }

        public ITextShapeRenderer New(Transform parent)
        {
            tempTransform = parent;
            return componentPool.Get()!;
        }
    }
}
