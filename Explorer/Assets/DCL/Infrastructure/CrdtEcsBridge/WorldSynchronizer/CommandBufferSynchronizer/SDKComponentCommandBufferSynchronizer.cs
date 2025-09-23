using Arch.Buffer;
using Arch.Core;
using CRDT.Protocol;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using JetBrains.Annotations;
using UnityEngine;

namespace CrdtEcsBridge.WorldSynchronizer.CommandBuffer
{
    /// <summary>
    ///     CommandBuffer has generic methods only, so we need to know the type of components somehow
    ///     This class resolves the issue
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
        ///     Applies deserialized component to the world.
        ///     Must be called on the thread where World is running
        /// </summary>
        public override void Apply(World world, PersistentCommandBuffer commandBuffer, Entity entity, CRDTReconciliationEffect reconciliationEffect, object component,
            bool isResultComponent)
        {
            // this is the cast we need
            var c = (T)component;

            switch (reconciliationEffect)
            {
                case CRDTReconciliationEffect.ComponentModified:
                    // if component is modified then return to the pool the existing one
                    // No need to add it to the command buffer as we already get the component by ref and can "override" it directly without overhead
                    if (isResultComponent)
                    {
                        ref T ct = ref world.TryGetRef<T>(entity, out bool hasComponent);

                        if (hasComponent)
                        {
                            componentPool.Release(ct);
                            ct = c;
                        }
                    }
                    else
                    {
                        ref T pointerToPrevObj = ref world.Get<T>(entity);
                        componentPool.Release(pointerToPrevObj);
                        pointerToPrevObj = c;
                    }

                    break;
                case CRDTReconciliationEffect.ComponentAdded:
                    Debug.Assert(!world.Has<T>(entity)); // Trace Assert from Arch will not work with Unity
                    commandBuffer.Add(entity, c);
                    break;
                case CRDTReconciliationEffect.ComponentDeleted:
                    // if component is deleted return to the pool the existing one
                    if (isResultComponent)
                    {
                        if (world.TryGet(entity, out T ct))
                        {
                            componentPool.Release(ct);
                            commandBuffer.Remove<T>(entity);
                        }
                    }
                    else
                    {
                        Debug.Assert(world.Has<T>(entity));
                        componentPool.Release(world.Get<T>(entity));
                        commandBuffer.Remove<T>(entity);
                    }

                    world.Get<RemovedComponents>(entity).Set.Add(typeof(T));
                    break;
            }
        }
    }

    public abstract class SDKComponentCommandBufferSynchronizer
    {
        public abstract void Apply(World world, PersistentCommandBuffer commandBuffer,
            Entity entity,
            CRDTReconciliationEffect reconciliationEffect, [CanBeNull] object component, bool isResultComponent);
    }
}
