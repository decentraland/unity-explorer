using DCL.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     The Initial Scene State for a single scene. Tells us whether ISS is unavailable,
    ///     served by an asset bundle, or served as a descriptor with per-asset bundles.
    ///     Built by <c>LoadISSDescriptorSystem</c> from the descriptor JSON + a HEAD probe of the
    ///     legacy ISS bundle. Cached per <c>GetISSDescriptor</c> intention (keyed by scene id).
    /// </summary>
    public class ISSDescriptor
    {
        public enum State { None, Bundle, Descriptor }

        public static readonly ISSDescriptor NONE = new (State.None, default);

        public State CurrentState { get; }
        public IReadOnlyList<ISSDescriptorAsset> Assets { get; }

        // hash -> how many times that hash appears in the descriptor (the cap for bridge slots)
        private readonly Dictionary<string, int> hashCapacity;

        // hash -> how many copies are currently parked in the bridge
        private readonly Dictionary<string, int> bridgedCount = new ();

        // Platform-suffixed hashes ({rawHash}{platform}) — matches the format the SDK GLTF loader uses
        // when requesting per-asset bundles. Lets PrepareAssetBundleLoadingParametersSystem do O(1)
        // "is this AB request one of the ISS hashes" checks in Bundle mode.
        private readonly HashSet<string> bundleAssetHashes;

        // Shared ISS bundle held ref-counted for the scene's lifetime in Bundle mode so the LOD path
        // and rewritten SDK GLTF requests both hit the cache. Released by Dereference().
        private AssetBundleData? assetBundle;

        public ISSDescriptor(State state, ISSDescriptorMetadata metadata)
        {
            CurrentState = state;
            // JsonUtility leaves the list null when the JSON field is missing — fall back to empty
            // so consumers can iterate Assets without a null guard.
            Assets = metadata.assets ?? new List<ISSDescriptorAsset>();
            hashCapacity = BuildHashCapacity(Assets);
            bundleAssetHashes = BuildBundleAssetHashes(Assets);
        }

        /// <summary>
        ///     Attempts to reserve a bridge slot for <paramref name="hash"/>. Caps at the number of times the hash
        ///     appears in the descriptor — so the bridge never holds more copies of an asset than the scene needs.
        ///     Returns true if reserved (caller should bridge the asset). Pair with <see cref="ReleaseBridgeSlot"/>
        ///     when the bridged copy leaves the cache.
        /// </summary>
        public bool TryReserveBridgeSlot(string hash)
        {
            if (CurrentState == State.None) return false;
            if (!hashCapacity.TryGetValue(hash, out int cap)) return false;

            int current = bridgedCount.TryGetValue(hash, out int n) ? n : 0;
            if (current >= cap) return false;

            bridgedCount[hash] = current + 1;
            return true;
        }

        /// <summary>
        ///     Releases a slot previously reserved via <see cref="TryReserveBridgeSlot"/>.
        ///     Call when a bridged copy is popped out of the cache (LOD pull) or evicted.
        /// </summary>
        public void ReleaseBridgeSlot(string hash)
        {
            if (bridgedCount.TryGetValue(hash, out int n) && n > 0)
                bridgedCount[hash] = n - 1;
        }

        /// <summary>
        ///     Whether the given AB request hash (already platform-suffixed) refers to an asset that should be
        ///     served by the shared ISS bundle. Only meaningful in <see cref="State.Bundle"/> — in any other
        ///     state the per-asset bundle URLs resolve normally.
        /// </summary>
        public bool IsBundleAsset(string platformSuffixedHash) =>
            CurrentState == State.Bundle && bundleAssetHashes.Contains(platformSuffixedHash);

        /// <summary>
        ///     Binds the shared ISS asset bundle to the descriptor's lifetime in Bundle mode. Held ref-counted
        ///     until <see cref="Dereference"/> fires on scene unload.
        /// </summary>
        public void AttachAssetBundle(AssetBundleData bundle)
        {
            assetBundle = bundle;
        }

        /// <summary>
        ///     Releases the shared ISS bundle reference (if any). Safe to call on <see cref="NONE"/> or on
        ///     descriptors with no bundle attached — it's a no-op in those cases.
        /// </summary>
        public void Dereference()
        {
            assetBundle?.Dereference();
            assetBundle = null;
        }

        public bool SupportsDescriptor() =>
            CurrentState == State.Descriptor;

        public bool SupportsBundle() =>
            CurrentState == State.Bundle;

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

        private static HashSet<string> BuildBundleAssetHashes(IReadOnlyList<ISSDescriptorAsset> assets)
        {
            string platform = PlatformUtils.GetCurrentPlatform();
            var set = new HashSet<string>(assets.Count);
            for (var i = 0; i < assets.Count; i++)
                set.Add($"{assets[i].hash}{platform}");
            return set;
        }
    }

    [Serializable]
    public struct ISSDescriptorMetadata
    {
        public List<ISSDescriptorAsset> assets;
    }

    [Serializable]
    public struct ISSDescriptorAsset
    {
        public string hash;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
}
