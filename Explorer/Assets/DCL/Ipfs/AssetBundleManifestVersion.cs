using DCL.Platforms;
using DCL.Utility;
using System;
using System.Collections.Generic;
using Utility;

namespace DCL.Ipfs
{
public class AssetBundleManifestVersion
    {
        //From v25 onwards, the asset bundle path contains the sceneID in the hash
        //This was done to solve cache issues
        private const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 25;

        //From v41 ISS is supported for scenes
        private const int ASSET_BUNDLE_VERSION_SUPPORTS_ISS = 41;

        public static readonly int AB_MIN_SUPPORTED_VERSION_WINDOWS = 15;
        public static readonly int AB_MIN_SUPPORTED_VERSION_MAC = 16;

        private bool? HasHashInPathValue;
        private bool? SupportsISS;


        public bool assetBundleManifestRequestFailed;
        public bool IsLSDAsset;
        public AssetBundleManifestVersionPerPlatform assets;

        private HashSet<string>? convertedFiles;

        public bool HasHashInPath()
        {
            if (HasHashInPathValue == null)
            {
                if (string.IsNullOrEmpty(GetAssetBundleManifestVersion()))
                    HasHashInPathValue = false;
                else
                    HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;
            }

            return HasHashInPathValue.Value;
        }

        public bool SupportsInitialSceneState()
        {
            if (SupportsISS == null)
            {
                if (string.IsNullOrEmpty(GetAssetBundleManifestVersion()))
                    SupportsISS = false;
                else
                    SupportsISS = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_SUPPORTS_ISS;
            }

            return SupportsISS.Value;
        }

        public string GetAssetBundleManifestVersion() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets.windows.version : assets.mac.version;

        public string GetAssetBundleManifestBuildDate() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets.windows.buildDate : assets.mac.buildDate;

        public bool IsEmpty() =>
            assets.IsEmpty();

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

        public static AssetBundleManifestVersion CreateManualManifest()
        {
            var assetBundleManifestVersion = new AssetBundleManifestVersion();
            var assets = new AssetBundleManifestVersionPerPlatform();
            assets.mac = new PlatformInfo(AB_MIN_SUPPORTED_VERSION_WINDOWS.ToString(), "1");
            assets.windows = new PlatformInfo(AB_MIN_SUPPORTED_VERSION_MAC.ToString(), "1");
            assetBundleManifestVersion.assets = assets;
            assetBundleManifestVersion.HasHashInPath();

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateFromFallback(string version, string buildDate)
        {
            var assets = new AssetBundleManifestVersionPerPlatform();
            assets.SetVersion(version, buildDate);

            var assetBundleManifestVersion = new AssetBundleManifestVersion();
            assetBundleManifestVersion.assets = assets;
            assetBundleManifestVersion.HasHashInPath();

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateForLOD(string assetBundleManifestVerison, string buildDate)
        {
            var assets = new AssetBundleManifestVersionPerPlatform();
            assets.SetVersion(assetBundleManifestVerison, buildDate);

            var assetBundleManifestVersion = new AssetBundleManifestVersion();
            assetBundleManifestVersion.assets = assets;
            assetBundleManifestVersion.HasHashInPathValue = false;

            return assetBundleManifestVersion;
        }

        public string CheckCasing(string inputHash)
        {
            if (convertedFiles == null || convertedFiles.Count == 0)
                return inputHash;

            if (convertedFiles.TryGetValue(inputHash, out string convertedFile))
                return convertedFile;

            return inputHash;
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
            ReadOnlySpan<char> span = entityID.AsSpan();
            bool isQM = span.Length >= 2 && span[0] == 'Q' && span[1] == 'm';

            if (isQM)
            {
                convertedFiles = new HashSet<string>(new UrlHashComparer());

                if (IPlatform.DEFAULT.Is(IPlatform.Kind.Mac))
                {
                    for (var i = 0; i < entityDefinitionContent.Length; i++)
                        convertedFiles.Add($"{entityDefinitionContent[i].hash.ToLowerInvariant()}" + PlatformUtils.GetCurrentPlatform());
                }

                if (IPlatform.DEFAULT.Is(IPlatform.Kind.Windows))
                {
                    for (var i = 0; i < entityDefinitionContent.Length; i++)
                        convertedFiles.Add($"{entityDefinitionContent[i].hash}" + PlatformUtils.GetCurrentPlatform());
                }
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
        public string version;
        public string buildDate;

        public PlatformInfo(string version, string buildDate)
        {
            this.version = version;
            this.buildDate = buildDate;
        }
    }
}
