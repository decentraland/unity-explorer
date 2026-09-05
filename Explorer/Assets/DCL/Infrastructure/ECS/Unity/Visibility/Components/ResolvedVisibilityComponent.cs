using Arch.Core;
using DCL.ECSComponents;

namespace ECS.Unity.Visibility.Components
{
    /// <summary>
    /// Stores the resolved/computed visibility for an entity.
    /// This takes into account both the entity's own PBVisibilityComponent (if any)
    /// and inherited visibility from ancestors with PropagateToChildren=true.
    /// </summary>
    public struct ResolvedVisibilityComponent : IDirtyMarker
    {
        /// <summary>
        /// The final computed visibility state for this entity.
        /// </summary>
        public bool IsVisible;

        /// <summary>
        /// The entity that is the source of this visibility (self or ancestor).
        /// Entity.Null means default visible (no visibility source).
        /// </summary>
        public Entity SourceEntity;

        /// <summary>
        /// Whether this visibility should cascade to children.
        /// Used when reparenting to check if new parent's visibility should propagate.
        /// </summary>
        public bool ShouldPropagate;

        /// <summary>
        /// Tracks the last known parent for detecting reparenting.
        /// Compared with TransformComponent.Parent when SDKTransform.IsDirty.
        /// </summary>
        public Entity LastKnownParent;

        /// <summary>
        /// Dirty flag to trigger visibility system updates.
        /// </summary>
        public bool IsDirty { get; set; }

        public static ResolvedVisibilityComponent CreateDefault() => new()
        {
            IsVisible = true,
            SourceEntity = Entity.Null,
            ShouldPropagate = false,
            LastKnownParent = Entity.Null,
            IsDirty = false
        };

        public static ResolvedVisibilityComponent CreateFromSource(Entity sourceEntity, bool isVisible, bool shouldPropagate, Entity parent) => new()
        {
            IsVisible = isVisible,
            SourceEntity = sourceEntity,
            ShouldPropagate = shouldPropagate,
            LastKnownParent = parent,
            IsDirty = true
        };
    }
}

