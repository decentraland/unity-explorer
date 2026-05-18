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

        public static readonly ISSDescriptor NONE = new (State.None, null);

        public State CurrentState { get; }
        public ISSDescriptorMetadata? Metadata { get; }

        private HashSet<string>? cachedAssetHashes;

        /// <summary>
        ///     Set of asset hashes referenced by this scene's ISS. Lazily built from <see cref="Metadata"/>.
        ///     Returns an empty set when the scene has no ISS.
        /// </summary>
        public HashSet<string> AssetHashes => cachedAssetHashes ??= BuildAssetHashes();

        /// <summary>
        ///     True if <paramref name="hash"/> is one of the assets covered by this scene's ISS.
        /// </summary>
        public bool IsPartOfISS(string hash) =>
            CurrentState != State.None && AssetHashes.Contains(hash);

        private ISSDescriptor(State state, ISSDescriptorMetadata? metadata)
        {
            CurrentState = state;
            Metadata = metadata;
        }

        private HashSet<string> BuildAssetHashes()
        {
            var set = new HashSet<string>();
            if (Metadata.HasValue && Metadata.Value.assets != null)
                foreach (var a in Metadata.Value.assets)
                    set.Add(a.hash);
            return set;
        }

        public bool SupportsDescriptor() =>
            CurrentState == State.Descriptor;

        public bool SupportsBundle() =>
            CurrentState == State.Bundle;

        public static ISSDescriptor NULL => new ISSDescriptor(State.None, null);
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
                metadata);
        }

        private static bool IsManifestVersionISSCapable(AssetBundleManifestVersion? manifestVersion)
        {
            if (manifestVersion == null || manifestVersion.assetBundleManifestRequestFailed) return false;

            string version = manifestVersion.GetAssetBundleManifestVersion();
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
                            .CreateFromJson<ISSDescriptorMetadata>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread);
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
