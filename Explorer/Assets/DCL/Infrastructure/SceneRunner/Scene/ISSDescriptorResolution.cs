using System;
using System.Collections.Generic;

namespace DCL.SceneRunner.Scene
{
    /// <summary>
    ///     Immutable result of resolving a scene's Initial Scene State: the final state
    ///     (Bundle / Descriptor / None) and the descriptor asset list. This is the value the
    ///     ISS loader produces and the disk cache stores — pure data, shareable across consumers,
    ///     no per-scene runtime state.
    ///     <para>
    ///     The resolver hands it off to the entity's <see cref="ISSDescriptor"/> component via
    ///     <see cref="ISSDescriptor.MarkResolved"/>; bridge-slot bookkeeping and lifecycle live on
    ///     the component, not here.
    ///     </para>
    /// </summary>
    public readonly struct ISSDescriptorResolution : IEquatable<ISSDescriptorResolution>
    {
        public static readonly ISSDescriptorResolution NONE = new (ISSDescriptorState.None, null);

        public readonly ISSDescriptorState State;
        public readonly IReadOnlyList<ISSDescriptorAsset>? Assets;

        public ISSDescriptorResolution(ISSDescriptorState state, IReadOnlyList<ISSDescriptorAsset>? assets)
        {
            State = state;
            Assets = assets;
        }

        public bool Equals(ISSDescriptorResolution other) =>
            State == other.State && Equals(Assets, other.Assets);

        public override bool Equals(object? obj) =>
            obj is ISSDescriptorResolution other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)State, Assets);

        public static bool operator ==(ISSDescriptorResolution left, ISSDescriptorResolution right)
            => left.Equals(right);

        public static bool operator !=(ISSDescriptorResolution left, ISSDescriptorResolution right)
            => !left.Equals(right);
    }
}
