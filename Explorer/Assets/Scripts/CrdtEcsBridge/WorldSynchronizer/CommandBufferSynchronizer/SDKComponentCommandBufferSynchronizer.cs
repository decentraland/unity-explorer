using Arch.Buffer;
using Arch.Core;
using CRDT.Protocol;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using System;
using UnityEngine;

namespace CrdtEcsBridge.WorldSynchronizer.CommandBufferSynchronizer
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
        public override void Apply(World world, PersistentCommandBuffer commandBuffer, Entity entity, CRDTReconciliationEffect reconciliationEffect, object? component)
        {
            // this is the cast we need
            T? c = (T?)component;

            switch (reconciliationEffect)
            {
                case CRDTReconciliationEffect.ComponentModified:
                    // if component is modified then return to the pool the existing one
                    // No need to add it to the command buffer as we already get the component by ref and can "override" it directly without overhead
                    ref T pointerToPrevObj = ref world.Get<T>(entity);
                    componentPool.Release(pointerToPrevObj);
                    pointerToPrevObj = c;
                    break;
                case CRDTReconciliationEffect.ComponentAdded:
                    Debug.Assert(!world.Has<T>(entity)); // Trace Assert from Arch will not work with Unity
                    commandBuffer.Add(entity, c);
                    break;
                case CRDTReconciliationEffect.ComponentDeleted:
                    try
                    {
                        // if component is deleted return to the pool the existing one
                        Debug.Assert(world.Has<T>(entity));
                        var ecsComponent = world.Get<T>(entity);
                        componentPool.Release(ecsComponent);
                        commandBuffer.Remove<T>(entity);
                        world.Get<RemovedComponents>(entity).Set.Add(typeof(T));
                    }
                    catch (Exception e)
                    {
                        throw new Exception(
                            $"Error while deleting component world id: {world.Id}, entity id: {entity.Id}, type {typeof(T).FullName}",
                            e
                        );
                    }

                    break;
                case CRDTReconciliationEffect.NoChanges: break;
                case CRDTReconciliationEffect.EntityDeleted: break;
                default: throw new ArgumentOutOfRangeException(nameof(reconciliationEffect), reconciliationEffect, null);
            }
        }
    }

    public abstract class SDKComponentCommandBufferSynchronizer
    {
        public abstract void Apply(World world, PersistentCommandBuffer commandBuffer,
            Entity entity,
            CRDTReconciliationEffect reconciliationEffect, object? component);
    }

    public class LogSDKComponentCommandBufferSynchronizer<T> : SDKComponentCommandBufferSynchronizer where T : class, new()
    {
        private readonly SDKComponentCommandBufferSynchronizer<T> origin;
        private readonly Action<string> log;

        public LogSDKComponentCommandBufferSynchronizer(SDKComponentCommandBufferSynchronizer<T> origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public override void Apply(World world, PersistentCommandBuffer commandBuffer, Entity entity, CRDTReconciliationEffect reconciliationEffect, object? component)
        {
            log($"SDKComponentCommandBufferSynchronizer Apply to world {world.Id}, entity {entity.Id}, effect {reconciliationEffect}, component {component}, type {typeof(T).FullName}");
            origin.Apply(world, commandBuffer, entity, reconciliationEffect, component);
        }
    }
}
