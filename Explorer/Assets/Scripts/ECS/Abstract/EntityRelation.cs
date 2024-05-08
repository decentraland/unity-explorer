using Arch.Core;
using Arch.SystemGroups;
using DCL.Optimization.Pools;
using ECS.Groups;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace ECS.Abstract
{
    /// <summary>
    ///     1. Stored as in the corresponding unique buffer <br />
    ///     2. Serves as an alternative to one-time events that should happen much more seldom to avoid expensive querying of the parent component (such as state change) <br />
    ///     3. It is safe to use data as it preserves throughout the current frame
    ///     4. The lifecycle is handled automatically, by the end of the frame events are cleaned-up
    /// </summary>
    public readonly struct EntityRelation<TEvent>
    {
        public readonly Entity Entity;
        public readonly TEvent Value;

        public EntityRelation(Entity entity, TEvent value)
        {
            Entity = entity;
            Value = value;
        }
    }

    public abstract class EntityEventBuffer : IDisposable
    {
        public abstract void Dispose();

        public abstract void Clear();
    }

    /// <summary>
    ///     1. Stored as a separate unique component on the unique entity per T <br />
    ///     2. Removes the cost of iterating on several archetypes/entities
    /// </summary>
    public class EntityEventBuffer<T> : EntityEventBuffer
    {
        private static ObjectPool<EntityEventBuffer<T>> pool;

        public delegate void ForEachDelegate(Entity entity, T @event);

        private readonly List<EntityRelation<T>> relations;

        public EntityEventBuffer(int initialCapacity)
        {
            relations = new List<EntityRelation<T>>(initialCapacity);
        }

        public static void Register(int initialCapacity)
        {
            Assert.IsNull(pool);

            pool = new ObjectPool<EntityEventBuffer<T>>(() => new EntityEventBuffer<T>(initialCapacity), actionOnRelease: buffer => buffer.Clear(),
                defaultCapacity: PoolConstants.SCENES_COUNT);
        }

        internal static EntityEventBuffer<T> Rent() =>
            pool.Get();

        /// <summary>
        ///     The delegate passed here should be cached to prevent allocations,
        ///     events are processed in the order they were added and in theory may contain the same entity several times
        /// </summary>
        /// <param name="action"></param>
        public void ForEach(ForEachDelegate action)
        {
            for (var i = 0; i < relations.Count; i++)
            {
                EntityRelation<T> relation = relations[i];
                action.Invoke(relation.Entity, relation.Value);
            }
        }

        public void Add(Entity entity, T @event)
        {
            relations.Add(new EntityRelation<T>(entity, @event));
        }

        /// <summary>
        ///     Events are cleared at the end of the frame,
        ///     you still can clear them manually if needed
        /// </summary>
        public override void Clear()
        {
            relations.Clear();
        }

        public override void Dispose()
        {
            pool.Release(this);
        }
    }

    public class EntityEventsBuilder
    {
        private List<EntityEventBuffer> buffers;

        public EntityEventBuffer<T> Rent<T>()
        {
            buffers ??= new List<EntityEventBuffer>(10);

            for (var i = 0; i < buffers.Count; i++)
            {
                if (buffers[i] is EntityEventBuffer<T> existingBuffer)
                    return existingBuffer;
            }

            var buffer = EntityEventBuffer<T>.Rent();
            buffers.Add(buffer);
            return buffer;
        }

        internal IReadOnlyList<EntityEventBuffer> Build() =>
            (IReadOnlyList<EntityEventBuffer>) buffers ?? Array.Empty<EntityEventBuffer>();
    }

    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ClearEntityEventsSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyList<EntityEventBuffer> eventBuffers;

        internal ClearEntityEventsSystem(World world, in EntityEventsBuilder builder) : base(world)
        {
            eventBuffers = builder.Build();
        }

        public override void Dispose()
        {
            foreach (EntityEventBuffer entityEventBuffer in eventBuffers)
                entityEventBuffer.Dispose();
        }

        protected override void Update(float t)
        {
            foreach (EntityEventBuffer entityEventBuffer in eventBuffers)
                entityEventBuffer.Clear();
        }
    }
}
