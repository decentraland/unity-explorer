using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace DCL.SceneRunner.Scene
{
    /// <summary>
    ///     Per-entity Initial Scene State component for a single scene. Class semantics — the same
    ///     reference lives on the entity for the scene's lifetime, with internal state mutating in place
    ///     when the resolver promise completes (see <see cref="MarkResolved"/>). Held on
    ///     <see cref="ISceneData.ISSDescriptor"/>.
    ///     <para>
    ///     Lifecycle: constructed in <see cref="ISSDescriptorState.Uninitialized"/> for scenes that may have
    ///     ISS, or in <see cref="ISSDescriptorState.None"/> for opt-outs (PX / static-pointer / smart-wearable
    ///     previews). The resolver hands a transient <see cref="ISSDescriptorResolution"/> (cacheable pure data)
    ///     to <see cref="MarkResolved"/>, which mutates the component in place so cached references elsewhere
    ///     (e.g. <c>OrderedDataManaged</c> in the radius system) see the update without re-fetching.
    ///     </para>
    /// </summary>
    public class ISSDescriptor
    {
        /// <summary>
        ///     Shared sentinel used by opt-out paths (PX / static-pointer / smart-wearable preview) so they
        ///     don't have to construct a fresh descriptor just to express "no ISS." Safe to share because
        ///     <see cref="ISSDescriptorState.None"/> descriptors have no per-scene mutable state. Scenes
        ///     that may have ISS construct their own fresh instance so <see cref="MarkResolved"/> can mutate
        ///     them in place without affecting other scenes.
        /// </summary>
        public static readonly ISSDescriptor NONE = new (ISSDescriptorResolution.NONE);

        public static ISSDescriptor CreateUninitialized() =>
            new (new ISSDescriptorResolution(ISSDescriptorState.Uninitialized, null));


        public ISSDescriptorState CurrentState { get; private set; }
        public IReadOnlyList<ISSDescriptorAsset> Assets { get; private set; }

        // hash -> how many times that hash appears in the descriptor (the cap for bridge slots)
        private readonly Dictionary<string, int> hashCapacity;

        // hash -> how many copies are currently parked in the bridge
        private readonly Dictionary<string, int> bridgedCount = new ();

        private ISSDescriptor(ISSDescriptorResolution resolution)
        {
            CurrentState = resolution.State;
            Assets = resolution.Assets ?? Array.Empty<ISSDescriptorAsset>();
            hashCapacity = new Dictionary<string, int>();
        }

        /// <summary>
        ///     Transitions the descriptor from <see cref="ISSDescriptorState.Uninitialized"/> to a resolved
        ///     state (Bundle / Descriptor / None). Mutates the instance in place — cached references see the
        ///     update without a refetch. Called by <c>ResolveISSDescriptorSystem</c> when the resolver promise
        ///     completes.
        /// </summary>
        public void MarkResolved(ISSDescriptorResolution resolution)
        {
            CurrentState = resolution.State;
            Assets = resolution.Assets ?? Array.Empty<ISSDescriptorAsset>();
            for (var i = 0; i < Assets.Count; i++)
            {
                string hash = Assets[i].hash;
                hashCapacity[hash] = hashCapacity.TryGetValue(hash, out int n) ? n + 1 : 1;
            }
        }

        public bool TryReserveBridgeSlot(string hash)
        {
            if (CurrentState is ISSDescriptorState.None or ISSDescriptorState.Uninitialized) return false;
            if (!hashCapacity.TryGetValue(hash, out int cap)) return false;

            int current = bridgedCount.TryGetValue(hash, out int n) ? n : 0;
            if (current >= cap) return false;

            bridgedCount[hash] = current + 1;
            return true;
        }

        public void ReleaseBridgeSlot(string hash)
        {
            if (bridgedCount.TryGetValue(hash, out int n) && n > 0)
            {
                bridgedCount[hash] = n - 1;
                return;
            }

            // Surface paired-release mismatches the first time they happen instead of waiting for "the
            // bridge never admits new copies" to come up as a bug report. Over-release silently clamps at 0
            // and looks correct, but leaves the count drifted upward against reservations that did happen.
            ReportHub.LogWarning(ReportCategory.SCENE_LOADING, $"ISSDescriptor.ReleaseBridgeSlot called for hash '{hash}' with no outstanding reservation — paired Reserve/Release mismatch.");
        }

        public bool SupportsDescriptor() =>
            CurrentState == ISSDescriptorState.Descriptor;
    }
}
