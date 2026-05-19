using DCL.SceneRunner.Scene;
using DCL.Utility;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     The Initial Scene State for a single scene. Tells us whether ISS is unavailable, served by
    ///     a shared asset bundle, or served as a descriptor with per-asset bundles. Built by
    ///     <see cref="LoadISSDescriptorSystem"/> from the descriptor JSON + a HEAD probe of the legacy
    ///     ISS bundle; held on <see cref="DCL.SceneRunner.Scene.ISceneData.ISSDescriptor"/> via the
    ///     <see cref="IISSDescriptor"/> interface so the SceneRunner layer doesn't need to reference
    ///     the ECS asmdef where this class lives.
    /// </summary>
    public class ISSDescriptor : IISSDescriptor
    {
        public static readonly ISSDescriptor NONE = new (IISSDescriptor.State.None, default);

        public IISSDescriptor.State CurrentState { get; }
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

        public ISSDescriptor(IISSDescriptor.State state, ISSDescriptorMetadata metadata)
        {
            CurrentState = state;
            // JsonUtility leaves the list null when the JSON field is missing — fall back to empty
            // so consumers can iterate Assets without a null guard.
            Assets = metadata.assets ?? new List<ISSDescriptorAsset>();
            hashCapacity = BuildHashCapacity(Assets);
            bundleAssetHashes = BuildBundleAssetHashes(Assets);
        }

        public bool TryReserveBridgeSlot(string hash)
        {
            if (CurrentState == IISSDescriptor.State.None) return false;
            if (!hashCapacity.TryGetValue(hash, out int cap)) return false;

            int current = bridgedCount.TryGetValue(hash, out int n) ? n : 0;
            if (current >= cap) return false;

            bridgedCount[hash] = current + 1;
            return true;
        }

        public void ReleaseBridgeSlot(string hash)
        {
            if (bridgedCount.TryGetValue(hash, out int n) && n > 0)
                bridgedCount[hash] = n - 1;
        }

        public bool IsBundleAsset(string platformSuffixedHash) =>
            CurrentState == IISSDescriptor.State.Bundle && bundleAssetHashes.Contains(platformSuffixedHash);

        /// <summary>
        ///     Binds the shared ISS asset bundle to the descriptor's lifetime in Bundle mode. Held ref-counted
        ///     until <see cref="Dereference"/> fires on scene unload. Impl-only; consumers across asmdef
        ///     boundaries don't need to know about <c>AssetBundleData</c>.
        /// </summary>
        public void AttachAssetBundle(AssetBundleData bundle)
        {
            assetBundle = bundle;
        }

        public void Dereference()
        {
            assetBundle?.Dereference();
            assetBundle = null;
        }

        public bool SupportsDescriptor() =>
            CurrentState == IISSDescriptor.State.Descriptor;

        public bool SupportsBundle() =>
            CurrentState == IISSDescriptor.State.Bundle;

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
}
