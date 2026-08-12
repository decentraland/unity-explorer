using DCL.Platforms;
using DCL.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.Ipfs
{
public class AssetBundleManifestVersion
    {
        //From v25 onwards, the asset bundle path contains the sceneID in the hash
        //This was done to solve cache issues
        private const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 25;

        //From v49 the manifest exposes a per-file deps digest we can key the cache by
        private const int ASSET_BUNDLE_VERSION_SUPPORTS_DEPS_DIGEST = 49;

        //ISS (Initial Scene State) descriptors are only baked starting from v49 — older
        //manifests can't have an ISS, so the descriptor lookup is short-circuited.
        private const int ASSET_BUNDLE_VERSION_SUPPORTS_ISS = 49;

        public static readonly int AB_MIN_SUPPORTED_VERSION_WINDOWS = 15;
        public static readonly int AB_MIN_SUPPORTED_VERSION_MAC = 16;

        //Shared sentinel for paths that require a manifest but have none; injections no-op on failed manifests, so the instance stays immutable.
        public static readonly AssetBundleManifestVersion FAILED = CreateFailed();

        private static readonly char[] FILE_NAME_SEPARATOR = { '_' };

        private bool? HasHashInPathValue;

        private bool? SupportsDepsDigestsValue;
        private bool? SupportsISSValue;
        public bool assetBundleManifestRequestFailed;
        public bool IsLSDAsset;
        public AssetBundleManifestVersionPerPlatform? assets;

        //Bare hash → CDN file name; fed by InjectDepsDigests (digest-bearing names) and InjectContent (Qm casing fixes).
        private Dictionary<string, string>? cdnFiles;

        //Set when the manifest's files[] were injected — only scenes fetch them. Reusable bundles live under the shared assets/ prefix and cache-key on version+hash; wearables/emotes stay entity-scoped and keep buildDate keying.
        private bool hasReusableAssets;

        private bool HasHashInPath()
        {
            HasHashInPathValue ??= TryParseVersionNumber(GetAssetBundleManifestVersion(), out int version) && version >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;
            return HasHashInPathValue.Value;
        }

        /// <summary>True when the manifest's version is v49 or newer — a pure version check; individual files may still carry no digest.</summary>
        public bool SupportsDepsDigests()
        {
            SupportsDepsDigestsValue ??= TryParseVersionNumber(GetAssetBundleManifestVersion(), out int version) && version >= ASSET_BUNDLE_VERSION_SUPPORTS_DEPS_DIGEST;
            return SupportsDepsDigestsValue.Value;
        }

        /// <summary>
        ///     True when the manifest's version is new enough to potentially have an ISS (Initial Scene State)
        ///     descriptor baked for the scene. Older manifests pre-date the feature and can be short-circuited
        ///     without touching the network.
        /// </summary>
        public bool SupportsISS()
        {
            if (assetBundleManifestRequestFailed) return false;
            SupportsISSValue ??= TryParseVersionNumber(GetAssetBundleManifestVersion(), out int version) && version >= ASSET_BUNDLE_VERSION_SUPPORTS_ISS;
            return SupportsISSValue.Value;
        }

        //Try parse is required to avoid throwing exceptions when the version is not in the expected format, which can happen for LODs in example
        private static bool TryParseVersionNumber(string? version, out int parsed)
        {
            parsed = 0;
            if (string.IsNullOrEmpty(version) || version.Length < 2 || version[0] != 'v')
                return false;
            return int.TryParse(version.AsSpan(1), out parsed);
        }

        /// <summary>
        ///     Stores the manifest's <c>files[]</c> — the verbatim names bundles live under on the CDN's shared
        ///     <c>assets/</c> prefix — keyed by the bare hash. Callers gate on <see cref="SupportsDepsDigests" />:
        ///     pre-v49 bundles are entity-scoped and must not be flagged reusable.
        /// </summary>
        public void InjectDepsDigests(string[]? files)
        {
            if (files == null || files.Length == 0) return;
            hasReusableAssets = true;

            foreach (string file in files)
            {
                if (string.IsNullOrEmpty(file)) continue;

                // Non-suffixed entries (build logs, folders) carry no hash to key by.
                string[] parts = file.Split(FILE_NAME_SEPARATOR, 2);
                if (parts.Length < 2) continue;

                cdnFiles ??= new Dictionary<string, string>(new UrlHashComparer());
                cdnFiles[parts[0]] = file;
            }
        }

        /// <summary>Translates a bare hash to the hash requested from the CDN: the canonical manifest file name when known (digest-bearing, correctly cased), otherwise the platform-suffixed bare hash.</summary>
        public string GetCdnRequestHash(string bareHash) =>
            TryGetCdnFileName(bareHash, out string fileName) ? fileName : $"{bareHash}{PlatformUtils.GetCurrentPlatform()}";

        /// <summary>Composes the upper-layer cache key (GLTF container, etc.): the canonical CDN file name when known, otherwise the bare hash.</summary>
        public string ComposeCacheKey(string hash) =>
            TryGetCdnFileName(hash, out string fileName) ? fileName : hash;

        /// <summary>Computes the Unity-cache key for a CDN request hash: reusable bundles key on version+hash — the digest travels inside the hash, so the cache is shareable across republishes — while wearables/emotes keep buildDate keying, as their bundles are republished in place.</summary>
        public Hash128 ComputeCacheHash(string hash) =>
            hasReusableAssets
                ? ComputeHashV49(hash, GetAssetBundleManifestVersion())
                : ComputeHashLegacy(hash, GetAssetBundleManifestBuildDate());

        /// <summary>
        ///     Builds the CDN-relative path for a request hash: reusable bundles live under the shared
        ///     <c>assets/</c> prefix (no entity segment), entity-scoped bundles (wearables/emotes, pre-v49
        ///     scenes) keep the legacy shapes. <paramref name="entityScoped" /> puts reusable bundles under
        ///     <c>{version}/{sceneID}/</c> instead — the only lane the local abgen sidecar serves
        ///     digest-bearing names from (its flat <c>assets/</c> lane resolves bare hashes against a
        ///     catalyst, which local-scene path-derived ids defeat).
        /// </summary>
        public string GetCdnRequestPath(string hash, string sceneID, bool entityScoped = false)
        {
            string version = GetAssetBundleManifestVersion();

            if (hasReusableAssets)
                return entityScoped ? $"{version}/{sceneID}/{hash}" : $"{version}/assets/{hash}";

            if (HasHashInPath())
                return $"{version}/{sceneID}/{hash}";

            return $"{version}/{hash}";
        }

        private static unsafe Hash128 ComputeHashV49(string hash, string version)
        {
            // The digest embedded in the hash replaces buildDate-based invalidation, keeping the cache shareable across CDN republishes; the delimiter prevents version/hash boundary collisions.
            ReadOnlySpan<char> hashSpan = hash.AsSpan();
            ReadOnlySpan<char> versionSpan = version.AsSpan();

            Span<char> builder = stackalloc char[versionSpan.Length + 1 + hashSpan.Length];
            versionSpan.CopyTo(builder);
            builder[versionSpan.Length] = '|';
            hashSpan.CopyTo(builder[(versionSpan.Length + 1)..]);

            fixed (char* ptr = builder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * builder.Length)); }
        }

        private static unsafe Hash128 ComputeHashLegacy(string hash, string buildDate)
        {
            // Byte-identical to the pre-v49 cache key so existing Unity-AB-cache entries keep hitting after upgrade.
            // The lack of a delimiter is a known theoretical collision risk (e.g. buildDate ending in 'X' vs. hash
            // starting with 'X') — accepted here, will be addressed when v49 adoption lets us retire this path.
            Span<char> hashBuilder = stackalloc char[buildDate.Length + hash.Length];
            buildDate.AsSpan().CopyTo(hashBuilder);
            hash.AsSpan().CopyTo(hashBuilder[buildDate.Length..]);

            fixed (char* ptr = hashBuilder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * hashBuilder.Length)); }
        }

        private bool TryGetCdnFileName(string bareHash, out string fileName)
        {
            if (cdnFiles != null && cdnFiles.TryGetValue(bareHash, out fileName!))
                return true;

            fileName = string.Empty;
            return false;
        }

        //! safe: every factory (CreateFromFallback, CreateFailed, CreateManualManifest, CreateForLOD) sets the current platform's info, and deserialized manifests carry both platforms.
        public string GetAssetBundleManifestVersion() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets?.windows!.version! : assets?.mac!.version!;

        //! safe: same factory invariant as GetAssetBundleManifestVersion.
        private string GetAssetBundleManifestBuildDate() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets?.windows!.buildDate! : assets?.mac!.buildDate!;

        public bool IsEmpty() =>
            assets?.IsEmpty() ?? true;

        private static AssetBundleManifestVersion CreateFailed()
        {
            //All AB requests will fail when this occurs; its a dead end
            var failedAssets = new AssetBundleManifestVersionPerPlatform();
            failedAssets.SetVersion("v1", "1");
            var assetBundleManifestVersion = new AssetBundleManifestVersion
            {
                assetBundleManifestRequestFailed = true,
                assets = failedAssets,
            };
            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateLSDAsset()
        {
            var assetBundleManifestVersion = new AssetBundleManifestVersion
            {
                IsLSDAsset = true,
            };

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateManualManifest(string assetBundleManifestVersionMac, string buildDateMac, string assetBundleManifestVersionWin, string buildDateWin)
        {
            var assetBundleManifestVersion = new AssetBundleManifestVersion();
            var assets = new AssetBundleManifestVersionPerPlatform
            {
                mac = new PlatformInfo(assetBundleManifestVersionMac, buildDateMac),
                windows = new PlatformInfo(assetBundleManifestVersionWin, buildDateWin),
            };
            assetBundleManifestVersion.assets = assets;
            assetBundleManifestVersion.HasHashInPath();

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateManualManifest()
        {
            var assetBundleManifestVersion = new AssetBundleManifestVersion();
            var assets = new AssetBundleManifestVersionPerPlatform
            {
                windows = new PlatformInfo(AB_MIN_SUPPORTED_VERSION_WINDOWS.ToString(), "1"),
                mac = new PlatformInfo(AB_MIN_SUPPORTED_VERSION_MAC.ToString(), "1"),
            };
            assetBundleManifestVersion.assets = assets;
            assetBundleManifestVersion.HasHashInPath();

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateFromFallback(string version, string buildDate)
        {
            var assets = new AssetBundleManifestVersionPerPlatform();
            assets.SetVersion(version, buildDate);

            var assetBundleManifestVersion = new AssetBundleManifestVersion { assets = assets };
            assetBundleManifestVersion.HasHashInPath();

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateForLOD(string assetBundleManifestVerison, string buildDate)
        {
            var assets = new AssetBundleManifestVersionPerPlatform();
            assets.SetVersion(assetBundleManifestVerison, buildDate);

            var assetBundleManifestVersion = new AssetBundleManifestVersion { assets = assets };

            return assetBundleManifestVersion;
        }

        public void InjectContent(string entityID, ContentDefinition[] entityDefinitionContent)
        {
            // A failed manifest serves no file names — and it can be the shared FAILED sentinel, which must never be mutated.
            if (assetBundleManifestRequestFailed) return;

            // TODO (JUANI): hack, for older Qm. Doesnt happen with bafk because they are all lowercase
            // This has a long due capitalization problem. The hash in Mac which is requested should always be lower case, since the output files are lowercase and the
            // request to S3 is case sensitive.
            // IE: This works: https://ab-cdn.decentraland.org/v35/Qmf7DaJZRygoayfNn5Jq6QAykrhFpQUr2us2VFvjREiajk/qmabrb8wisg9b4szzt6achgajdyultejpzmtwdi4rcetzv_mac
            //     This doesnt: https://ab-cdn.decentraland.org/v35/Qmf7DaJZRygoayfNn5Jq6QAykrhFpQUr2us2VFvjREiajk/QmaBrb8WisG9b4Szzt6ACHgaJdyULTEjpzmTwDi4RCEtZV_mac
            // This was previously fixes using this extension (https://github.com/decentraland/unity-explorer/blob/7dd332562143e406fecf7006ac86586add0b0c71/Explorer/Assets/DCL/Infrastructure/SceneRunner/Scene/SceneAssetBundleManifestExtensions.cs#L5)
            // But we cannot use it anymore since we are not downloading the whole manifest
            // Whatsmore, the dependencies inside Qm files are always lowercase. But in Windows, files are case dependant. So, Windows also needs to handle this sepcial cases
            // Maybe one day, when `Qm` deployments dont exist anymore, this method can be removed
            if (!AssetBundleManifestHelper.IsQmEntity(entityID)) return;

            cdnFiles ??= new Dictionary<string, string>(new UrlHashComparer());
            string platformSuffix = PlatformUtils.GetCurrentPlatform();
            bool lowerCase = IPlatform.DEFAULT.Is(IPlatform.Kind.Mac);

            // TryAdd keeps the first entry for each key; a digest-bearing name already stored by InjectDepsDigests is never overwritten.
            for (var i = 0; i < entityDefinitionContent.Length; i++)
            {
                string hash = entityDefinitionContent[i].hash;
                cdnFiles.TryAdd(hash, (lowerCase ? hash.ToLowerInvariant() : hash) + platformSuffix);
            }
        }
    }

    public class AssetBundleManifestVersionPerPlatform
    {
        public PlatformInfo? mac;
        public PlatformInfo? windows;

        public void SetVersion(string assetBundleManifestVersion, string buildDate)
        {
            if (IPlatform.DEFAULT.Is(IPlatform.Kind.Windows))
                windows = new PlatformInfo(assetBundleManifestVersion, buildDate);
            else
                mac = new PlatformInfo(assetBundleManifestVersion, buildDate);
        }

        public bool IsEmpty()
        {
            if (IPlatform.DEFAULT.Is(IPlatform.Kind.Windows))
                return windows == null || string.IsNullOrEmpty(windows.version);

            return mac == null || string.IsNullOrEmpty(mac.version);
        }
    }

    public class PlatformInfo
    {
        public readonly string version;
        public readonly string buildDate;

        public PlatformInfo(string version, string buildDate)
        {
            this.version = version;
            this.buildDate = buildDate;
        }
    }
}
