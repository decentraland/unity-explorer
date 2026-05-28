using DCL.Diagnostics;
using DCL.SceneRunner.Scene;
using DCL.Utility;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     The Initial Scene State for a single scene. Attached as its own ECS component on the scene
    ///     entity (class semantics — same reference for the scene's lifetime, internal state mutates).
    ///     Built by <see cref="LoadISSDescriptorSystem"/> from the descriptor JSON; held on
    ///     <see cref="DCL.SceneRunner.Scene.ISceneData.ISSDescriptor"/> via the <see cref="IISSDescriptor"/>
    ///     interface so the SceneRunner layer doesn't need to reference the ECS asmdef where this class lives.
    ///     <para>
    ///     Lifecycle: constructed in <see cref="IISSDescriptor.State.Uninitialized"/> for scenes that may have
    ///     ISS, or in <see cref="IISSDescriptor.State.None"/> for opt-outs (PX / static-pointer / smart-wearable
    ///     previews). The resolver mutates state in place via <see cref="MarkResolved"/> so cached references
    ///     elsewhere (e.g. <c>OrderedDataManaged</c> in the radius system) see updates without re-fetching.
    ///     </para>
    /// </summary>
    public class ISSDescriptor : IISSDescriptor
    {
        /// <summary>
        ///     Shared sentinel used by the resolver as the result for "this scene has no ISS." Safe to share
        ///     because <see cref="IISSDescriptor.State.None"/> descriptors have no per-scene mutable state
        ///     (bridge slots, asset bundle, etc. are all short-circuited). Per-scene component instances on
        ///     the entity should NOT be this singleton — they're constructed fresh so MarkResolved can mutate
        ///     them in place without affecting other scenes.
        /// </summary>
        public static readonly ISSDescriptor NONE = new (IISSDescriptor.State.None, default);

        public IISSDescriptor.State CurrentState { get; private set; }
        public IReadOnlyList<ISSDescriptorAsset> Assets { get; private set; }

        // hash -> how many times that hash appears in the descriptor (the cap for bridge slots)
        private Dictionary<string, int> hashCapacity;

        // hash -> how many copies are currently parked in the bridge
        private readonly Dictionary<string, int> bridgedCount = new ();

        /// <summary>Constructs a fresh descriptor in <see cref="IISSDescriptor.State.Uninitialized"/>.</summary>
        public ISSDescriptor() : this(IISSDescriptor.State.Uninitialized, default) { }

        public ISSDescriptor(IISSDescriptor.State state, ISSDescriptorMetadata metadata)
        {
            CurrentState = state;
            // JsonUtility leaves the list null when the JSON field is missing — fall back to empty
            // so consumers can iterate Assets without a null guard.
            Assets = metadata.assets ?? new List<ISSDescriptorAsset>();
            hashCapacity = BuildHashCapacity(Assets);
        }

        /// <summary>
        ///     Transitions the descriptor from <see cref="IISSDescriptor.State.Uninitialized"/> to a resolved
        ///     state (Bundle / Descriptor / None). Mutates the instance in place — cached references see the
        ///     update without a refetch. Called by <c>ResolveISSDescriptorSystem</c> when the resolver promise
        ///     completes.
        /// </summary>
        public void MarkResolved(IISSDescriptor.State state, ISSDescriptorMetadata metadata)
        {
            CurrentState = state;
            Assets = metadata.assets ?? new List<ISSDescriptorAsset>();
            hashCapacity = BuildHashCapacity(Assets);
        }

        public bool TryReserveBridgeSlot(string hash)
        {
            if (CurrentState is IISSDescriptor.State.None or IISSDescriptor.State.Uninitialized) return false;
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
            CurrentState == IISSDescriptor.State.Descriptor;


        private static Dictionary<string, int> BuildHashCapacity(IReadOnlyList<ISSDescriptorAsset> assets)
        {
            var counts = new Dictionary<string, int>(assets.Count);
            for (var i = 0; i < assets.Count; i++)
            {
                string hash = assets[i].hash;
                counts[hash] = counts.TryGetValue(hash, out int n) ? n + 1 : 1;
            }
            return counts;
        }

    }

    [Serializable]
    public struct ISSDescriptorMetadata
    {
        public List<ISSDescriptorAsset> assets;
    }
}
