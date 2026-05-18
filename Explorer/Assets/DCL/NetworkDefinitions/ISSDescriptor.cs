using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utility;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Application = UnityEngine.Application;

namespace DCL.Ipfs
{
    /// <summary>
    ///     The Initial Scene State for a single scene. Tells us whether ISS is unavailable,
    ///     served by an asset bundle, or served as a descriptor with per-asset bundles.
    ///     Built by <see cref="ResolveAsync"/> during scene-definition loading and stored on
    ///     <see cref="SceneEntityDefinition.ISSDescriptor"/>.
    /// </summary>
    public class ISSDescriptor
    {
        public enum State { None, Bundle, Descriptor }

        private const string DESCRIPTOR_SUBDIR = "iss_descriptors";

        // ISS is only baked starting from AB manifest version 49. Anything older cannot have ISS,
        // so we skip the descriptor fetch + HEAD probe entirely for those scenes.
        private const int MIN_ISS_AB_VERSION = 49;

        // Hardcoded for this test iteration — wire to DI later.
        private static readonly URLDomain STREAMING_ASSET_URL =
            URLDomain.FromString(
#if UNITY_EDITOR || UNITY_STANDALONE
                $"file://{Application.streamingAssetsPath}/AssetBundles/"
#else
                $"{Application.streamingAssetsPath}/AssetBundles/"
#endif
            );

        private static readonly URLDomain ASSET_BUNDLE_URL = URLDomain.FromString("https://ab-cdn.decentraland.org");

        public static readonly ISSDescriptor NONE = new (State.None, default);

        public State CurrentState { get; }
        public IReadOnlyList<ISSDescriptorAsset> Assets { get; }

        // hash -> how many times that hash appears in Metadata.assets (the cap for bridge slots)
        private readonly Dictionary<string, int> hashCapacity;

        // hash -> how many copies are currently parked in the bridge
        private readonly Dictionary<string, int> bridgedCount = new ();

        // Platform-suffixed hashes ({rawHash}{platform}) — matches the format the SDK GLTF loader uses
        // when requesting per-asset bundles. Lets PrepareAssetBundleLoadingParametersSystem do O(1)
        // "is this AB request one of the ISS hashes" checks in Bundle mode.
        private readonly HashSet<string> bundleAssetHashes;

        // Cleanup callback bound to the descriptor's lifetime (typically AssetBundleData.Dereference for
        // the shared ISS bundle in Bundle mode). Typed as Action rather than AssetBundleData to avoid an
        // asmdef cycle between DCL.Network and ECS. Invoked exactly once via Dereference().
        private Action? releaseAssetBundle;

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

        private ISSDescriptor(State state, ISSDescriptorMetadata metadata)
        {
            CurrentState = state;
            // JsonUtility leaves the list null when the JSON field is missing — fall back to empty
            // so consumers can iterate Assets without a null guard.
            Assets = metadata.assets ?? new List<ISSDescriptorAsset>();
            hashCapacity = BuildHashCapacity(Assets);
            bundleAssetHashes = BuildBundleAssetHashes(Assets);
        }

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

        /// <summary>
        ///     Whether the given AB request hash (already platform-suffixed) refers to an asset that should be
        ///     served by the shared ISS bundle. Only meaningful in <see cref="State.Bundle"/> — in any other
        ///     state the per-asset bundle URLs resolve normally.
        /// </summary>
        public bool IsBundleAsset(string platformSuffixedHash) =>
            CurrentState == State.Bundle && bundleAssetHashes.Contains(platformSuffixedHash);

        /// <summary>
        ///     Binds the shared ISS asset bundle to the descriptor's lifetime in Bundle mode. The provided
        ///     callback (typically <c>AssetBundleData.Dereference</c>) fires once on <see cref="Dereference"/>
        ///     so the bundle stays cached while the scene is alive and is released when it unloads.
        /// </summary>
        public void AttachAssetBundle(Action release)
        {
            releaseAssetBundle = release;
        }

        /// <summary>
        ///     Releases the bundle bound via <see cref="AttachAssetBundle"/>. Safe to call on <see cref="NONE"/>
        ///     or on descriptors with no bundle attached — it's a no-op in those cases.
        /// </summary>
        public void Dereference()
        {
            releaseAssetBundle?.Invoke();
            releaseAssetBundle = null;
        }

        public bool SupportsDescriptor() =>
            CurrentState == State.Descriptor;

        public bool SupportsBundle() =>
            CurrentState == State.Bundle;

        /// <summary>
        ///     Looks up the ISS state for the given scene: tries to fetch the descriptor JSON,
        ///     then HEAD-probes the legacy ISS asset bundle. Returns <see cref="NONE"/> if no descriptor exists.
        /// </summary>
        public static async UniTask<ISSDescriptor> ResolveAsync(
            SceneEntityDefinition sceneDefinition,
            IWebRequestController webRequestController,
            ReportData reportCategory,
            CancellationToken ct)
        {
            // Skip the network roundtrips entirely for AB versions that pre-date ISS support.
            if (!IsManifestVersionISSCapable(sceneDefinition.assetBundleManifestVersion))
                return NONE;

            ISSDescriptorMetadata? metadata = await TryLoadDescriptorAsync(sceneDefinition.id, webRequestController, reportCategory, ct);
            if (!metadata.HasValue) return NONE;

            bool bundleReachable = await IsBundleReachableAsync(sceneDefinition, webRequestController, reportCategory, ct);

            return new ISSDescriptor(
                bundleReachable ? State.Bundle : State.Descriptor,
                metadata.Value);
        }

        private static bool IsManifestVersionISSCapable(AssetBundleManifestVersion? manifestVersion)
        {
            if (manifestVersion == null || manifestVersion.assetBundleManifestRequestFailed) return false;

            string? version = manifestVersion.GetAssetBundleManifestVersion();
            if (string.IsNullOrEmpty(version) || version.Length < 2) return false;

            // Versions look like "v41" — strip the leading 'v' and compare numerically.
            return int.TryParse(version.AsSpan().Slice(1), out int versionNum) && versionNum >= MIN_ISS_AB_VERSION;
        }

        private static async UniTask<ISSDescriptorMetadata?> TryLoadDescriptorAsync(
            string sceneID,
            IWebRequestController webRequestController,
            ReportData reportCategory,
            CancellationToken ct)
        {
            URLAddress url = STREAMING_ASSET_URL.Append(URLPath.FromString($"{DESCRIPTOR_SUBDIR}/{sceneID}_InitialSceneState.json"));

            try
            {
                return await webRequestController
                            .GetAsync(new CommonArguments(url), ct, reportCategory)
                            .CreateFromJson<ISSDescriptorMetadata>(WRJsonParser.Unity);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // No descriptor at the expected path — treat as no ISS.
                return null;
            }
        }

        private static async UniTask<bool> IsBundleReachableAsync(
            SceneEntityDefinition definition,
            IWebRequestController webRequestController,
            ReportData reportCategory,
            CancellationToken ct)
        {
            var manifestVersion = definition.assetBundleManifestVersion;
            if (manifestVersion == null || manifestVersion.assetBundleManifestRequestFailed) return false;

            string version = manifestVersion.GetAssetBundleManifestVersion();
            if (string.IsNullOrEmpty(version)) return false;

            string bundleHash = $"staticscene_{definition.id}{PlatformUtils.GetCurrentPlatform()}";
            URLAddress bundleUrl = manifestVersion.HasHashInPath()
                ? ASSET_BUNDLE_URL.Append(new URLPath($"{version}/{definition.id}/{bundleHash}"))
                : ASSET_BUNDLE_URL.Append(new URLPath($"{version}/{bundleHash}"));

            return await webRequestController.IsHeadReachableAsync(reportCategory, bundleUrl, ct, suppressErrors: true);
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
