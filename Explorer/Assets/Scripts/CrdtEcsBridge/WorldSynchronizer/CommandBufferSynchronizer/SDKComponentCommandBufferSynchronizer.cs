using Arch.Core;
using CRDT.Protocol;
using ECS.ComponentsPooling;
using JetBrains.Annotations;

namespace CrdtEcsBridge.WorldSynchronizer.CommandBuffer
{
    /// <summary>
    /// CommandBuffer has generic methods only, so we need to know the type of components somehow
    /// This class resolves the issue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SDKComponentCommandBufferSynchronizer<T> : SDKComponentCommandBufferSynchronizer where T: class, new()
    {
        private readonly IComponentPool<T> componentPool;

        public SDKComponentCommandBufferSynchronizer(IComponentPool<T> componentPool)
        {
            this.componentPool = componentPool;
        }

        /// <summary>
        /// Applies deserialized component to the world.
        /// Must be called on the thread where World is running
        /// </summary>
        public override void Apply(World world, Arch.CommandBuffer.CommandBuffer commandBuffer, Entity entity, CRDTReconciliationEffect reconciliationEffect, object component)
        {
            // this is the cast we need
            var c = (T)component;

            switch (reconciliationEffect)
            {
                case CRDTReconciliationEffect.ComponentModified:
                    // if component is modified then return to the pool the existing one
                    componentPool.Release(world.Get<T>(entity));
                    commandBuffer.Set(entity, c);
                    break;
                case CRDTReconciliationEffect.ComponentAdded:
                    commandBuffer.Add(entity, c);
                    break;
                case CRDTReconciliationEffect.ComponentDeleted:
                    // if component is deleted return to the pool the existing one
                    componentPool.Release(world.Get<T>(entity));
                    commandBuffer.Remove<T>(entity);
                    break;
            }
        }
    }

    public abstract class SDKComponentCommandBufferSynchronizer
    {
        public abstract void Apply(World world, Arch.CommandBuffer.CommandBuffer commandBuffer,
            Entity entity,
            CRDTReconciliationEffect reconciliationEffect, [CanBeNull] object component);
    }
}
