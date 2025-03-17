using Arch.Core;
using System;
using System.Collections.Generic;

namespace ECS.Abstract
{
    /// <summary>
    ///     1. Stored as in the corresponding unique buffer <br />
    ///     2. Serves as an alternative to one-time events that should happen much more seldom to avoid expensive querying of the parent component (such as state change) <br />
    ///     3. It is safe to use data as it preserves throughout the current frame
    ///     4. The lifecycle is handled automatically, by the end of the frame events are cleaned-up
    /// </summary>
    public readonly struct EntityRelation<TEvent> : IEquatable<EntityRelation<TEvent>>
    {
        public readonly Entity Entity;
        public readonly TEvent Value;

        public EntityRelation(Entity entity, TEvent value)
        {
            Entity = entity;
            Value = value;
        }

        public bool Equals(EntityRelation<TEvent> other) =>
            Entity.Equals(other.Entity) && EqualityComparer<TEvent>.Default.Equals(Value, other.Value);

        public override bool Equals(object obj) =>
            obj is EntityRelation<TEvent> other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Entity, Value);
    }
}
