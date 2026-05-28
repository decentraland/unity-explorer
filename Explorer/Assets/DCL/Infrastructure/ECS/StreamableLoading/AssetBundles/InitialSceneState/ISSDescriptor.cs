using DCL.Diagnostics;
using DCL.SceneRunner.Scene;
using DCL.Utility;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Per-entity Initial Scene State component for a single scene. Class semantics — the same
    ///     reference lives on the entity for the scene's lifetime, with internal state mutating in place
    ///     when the resolver promise completes (see <see cref="MarkResolved"/>). Held on
    ///     <see cref="DCL.SceneRunner.Scene.ISceneData.ISSDescriptor"/> via the <see cref="IISSDescriptor"/>
    ///     interface so the SceneRunner layer doesn't need to reference the ECS asmdef where this class lives.
    ///     <para>
    ///     Lifecycle: constructed in <see cref="IISSDescriptor.State.Uninitialized"/> for scenes that may have
    ///     ISS, or in <see cref="IISSDescriptor.State.None"/> for opt-outs (PX / static-pointer / smart-wearable
    ///     previews). The resolver hands a transient <see cref="ISSDescriptorResolution"/> (cacheable pure data)
    ///     to <see cref="MarkResolved"/>, which mutates the component in place so cached references elsewhere
    ///     (e.g. <c>OrderedDataManaged</c> in the radius system) see the update without re-fetching.
    ///     </para>
    /// </summary>
    public class ISSDescriptor : IISSDescriptor
    {
        /// <summary>
        ///     Shared sentinel used by opt-out paths (PX / static-pointer / smart-wearable preview) so they
        ///     don't have to construct a fresh descriptor just to express "no ISS." Safe to share because
        ///     <see cref="IISSDescriptor.State.None"/> descriptors have no per-scene mutable state. Scenes
        ///     that may have ISS construct their own fresh instance so <see cref="MarkResolved"/> can mutate
        ///     them in place without affecting other scenes.
        /// </summary>
        public static readonly ISSDescriptor NONE = new (ISSDescriptorResolution.NONE);

        public IISSDescriptor.State CurrentState { get; private set; }
        public IReadOnlyList<ISSDescriptorAsset> Assets { get; private set; }

        // hash -> how many times that hash appears in the descriptor (the cap for bridge slots)
        private Dictionary<string, int> hashCapacity;

        // hash -> how many copies are currently parked in the bridge
        private readonly Dictionary<string, int> bridgedCount = new ();

        /// <summary>Constructs a fresh descriptor in <see cref="IISSDescriptor.State.Uninitialized"/>.</summary>
        public ISSDescriptor() : this(new ISSDescriptorResolution(IISSDescriptor.State.Uninitialized, null)) { }

        public ISSDescriptor(ISSDescriptorResolution resolution)
        {
            CurrentState = resolution.State;
            // JsonUtility / opt-out construction leave the list null — fall back to empty so consumers
            // can iterate Assets without a null guard.
            Assets = resolution.Assets ?? Array.Empty<ISSDescriptorAsset>();
            hashCapacity = BuildHashCapacity(Assets);
        }

        /// <summary>
        ///     Transitions the descriptor from <see cref="IISSDescriptor.State.Uninitialized"/> to a resolved
        ///     state (Bundle / Descriptor / None). Mutates the instance in place — cached references see the
        ///     update without a refetch. Called by <c>ResolveISSDescriptorSystem</c> when the resolver promise
        ///     completes.
        /// </summary>
        public void MarkResolved(ISSDescriptorResolution resolution)
        {
            CurrentState = resolution.State;
            Assets = resolution.Assets ?? Array.Empty<ISSDescriptorAsset>();
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
