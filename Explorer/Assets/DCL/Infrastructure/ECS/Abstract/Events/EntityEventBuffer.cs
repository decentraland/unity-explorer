using Arch.Core;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace ECS.Abstract
{
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

        public IReadOnlyList<EntityRelation<T>> Relations => relations;

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

    public abstract class EntityEventBuffer : IDisposable
    {
        public abstract void Dispose();

        public abstract void Clear();
    }
}
