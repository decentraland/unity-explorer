using DCL.Platforms;
using DCL.Utility;
using System;
using System.Collections.Generic;
using Utility;

// ReSharper disable once CheckNamespace
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

        private static readonly char[] FILE_NAME_SEPARATOR = { '_' };

        private bool? HasHashInPathValue;

        private bool? SupportsDepsDigestsValue;
        private bool? SupportsISSValue;
        public bool assetBundleManifestRequestFailed;
        public bool IsLSDAsset;
        public AssetBundleManifestVersionPerPlatform? assets;

        //Digest-less platform-suffixed hash → canonical CDN file name; fed by InjectDepsDigests (digest-bearing names) and InjectContent (Qm casing fixes).
        private Dictionary<string, string>? cdnFiles;
        private bool hasDepsDigests;

        public bool HasHashInPath()
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

        /// <summary>Stores, keyed by the digest-less platform-suffixed hash, the verbatim digest-bearing file name (<c>&lt;hash&gt;_&lt;depsDigest&gt;_&lt;platform&gt;</c>) from the manifest's <c>files[]</c>; legacy 2-part names are skipped.</summary>
        public void InjectDepsDigests(string[]? files)
        {
            if (files == null || files.Length == 0) return;

            foreach (string file in files)
            {
                if (string.IsNullOrEmpty(file)) continue;

                string[] parts = file.Split(FILE_NAME_SEPARATOR, 3);
                if (parts.Length < 3) continue;

                cdnFiles ??= new Dictionary<string, string>(new UrlHashComparer());
                cdnFiles[$"{parts[0]}_{parts[2]}"] = file;
                hasDepsDigests = true;
            }
        }

        /// <summary>True when digest-bearing files were injected — only scene manifests fetch <c>files[]</c>; wearables/emotes never do and keep buildDate cache keying.</summary>
        public bool HasDepsDigests() =>
            hasDepsDigests;

        /// <summary>Builds the hash requested from the CDN: the canonical manifest file name when known (digest-bearing, correctly cased), otherwise the platform-suffixed bare hash.</summary>
        public static string GetCdnRequestHash(AssetBundleManifestVersion? manifest, string bareHash) =>
            manifest?.ResolveCdnRequestHash(bareHash) ?? $"{bareHash}{PlatformUtils.GetCurrentPlatform()}";

        /// <summary>Composes the upper-layer cache key (GLTF container, etc.): the canonical CDN file name when known, otherwise the bare hash.</summary>
        public static string ComposeCacheKey(AssetBundleManifestVersion? manifest, string hash) =>
            manifest != null && manifest.TryResolveCdnRequestHash($"{hash}{PlatformUtils.GetCurrentPlatform()}", out string fileName) ? fileName : hash;

        /// <summary>Resolves a platform-suffixed hash to the canonical CDN file name — digest-bearing and correctly cased when known (case-insensitive lookup) — or returns the input unchanged when unlisted.</summary>
        public string ResolveCdnRequestHash(string hash) =>
            TryResolveCdnRequestHash(hash, out string fileName) ? fileName : hash;

        /// <summary>Same resolution as <see cref="ResolveCdnRequestHash" />, reporting whether the manifest knows the file.</summary>
        bool TryResolveCdnRequestHash(string hash, out string fileName)
        {
            if (cdnFiles != null && cdnFiles.TryGetValue(hash, out fileName!))
                return true;

            fileName = string.Empty;
            return false;
        }

        public string? GetAssetBundleManifestVersion() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets?.windows!.version : assets?.mac!.version;

        public string? GetAssetBundleManifestBuildDate() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets?.windows!.buildDate : assets?.mac!.buildDate;

        public bool IsEmpty() =>
            assets?.IsEmpty() ?? true;

        public static AssetBundleManifestVersion CreateFailed()
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
                mac = new PlatformInfo(AB_MIN_SUPPORTED_VERSION_WINDOWS.ToString(), "1"),
                windows = new PlatformInfo(AB_MIN_SUPPORTED_VERSION_MAC.ToString(), "1"),
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

            // TryAdd so digest-bearing entries from InjectDepsDigests always win, regardless of injection order.
            for (var i = 0; i < entityDefinitionContent.Length; i++)
            {
                string fileName = (lowerCase ? entityDefinitionContent[i].hash.ToLowerInvariant() : entityDefinitionContent[i].hash) + platformSuffix;
                cdnFiles.TryAdd(fileName, fileName);
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
