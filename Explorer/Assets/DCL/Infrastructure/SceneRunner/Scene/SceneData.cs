using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Utility;

namespace SceneRunner.Scene
{
    public class SceneData : ISceneData
    {
        /// <summary>
        ///     https://github.com/decentraland/unity-renderer/pull/5844
        /// </summary>
        private const bool CHECK_ALLOWED_MEDIA_HOSTNAMES =
#if CHECK_ALLOWED_MEDIA_HOSTNAMES
            true;
#else
            false;
#endif

        public ISceneContent SceneContent { get; }

        public SceneEntityDefinition SceneEntityDefinition { get; }

        public StaticSceneMessages StaticSceneMessages { get; }
        public bool SceneLoadingConcluded { get; set; }
        public SceneShortInfo SceneShortInfo { get; }
        public ParcelMathHelper.SceneGeometry Geometry { get; }
        public SceneAssetBundleManifest AssetBundleManifest { get; }
        public IReadOnlyList<Vector2Int> Parcels { get; }

        public SceneData(
            ISceneContent sceneContent,
            SceneEntityDefinition sceneDefinition,
            Vector2Int baseParcel,
            ParcelMathHelper.SceneGeometry geometry,
            IReadOnlyList<Vector2Int> parcels,
            StaticSceneMessages staticSceneMessages)
        {
            SceneContent = sceneContent;
            SceneEntityDefinition = sceneDefinition;
            StaticSceneMessages = staticSceneMessages;
            Parcels = parcels;
            SceneShortInfo = new SceneShortInfo(baseParcel, sceneDefinition.id, sceneDefinition.metadata.sdkVersion);
            Geometry = geometry;
        }

        public bool HasRequiredPermission(string permission)
        {
            if (SceneEntityDefinition.metadata.requiredPermissions == null)
                return false;

            foreach (string requiredPermission in SceneEntityDefinition.metadata.requiredPermissions)
            {
                if (requiredPermission == permission)
                    return true;
            }

            return false;
        }

        public bool TryGetMainScriptUrl(out URLAddress result) =>
            TryGetContentUrl(SceneEntityDefinition.metadata.main, out result);

        public bool TryGetContentUrl(string url, out URLAddress result) =>
            SceneContent.TryGetContentUrl(url, out result);

        public bool TryGetHash(string name, out string hash) =>
            SceneContent.TryGetHash(name, out hash);

        public bool TryGetMediaUrl(string url, out URLAddress result)
        {
            if (string.IsNullOrEmpty(url))
            {
                result = URLAddress.EMPTY;
                return false;
            }

            // Try resolve an internal URL
            if (TryGetContentUrl(url, out result))
                return true;

            bool isAllowed = CHECK_ALLOWED_MEDIA_HOSTNAMES
                ? HasRequiredPermission(ScenePermissionNames.ALLOW_MEDIA_HOSTNAMES) // permission gate
                  && IsUrlDomainAllowed(url) // whitelist
                : Uri.TryCreate(url, UriKind.Absolute, out _); // general syntax check

            if (isAllowed)
            {
                result = URLAddress.FromString(url);
                return true;
            }

            result = URLAddress.EMPTY;
            return false;
        }

        public bool TryGetMediaFileHash(string url, out string fileHash)
        {
            if (string.IsNullOrEmpty(url))
            {
                fileHash = string.Empty;
                return false;
            }

            // Try resolve an internal URL
            if (TryGetHash(url, out fileHash))
                return true;

            fileHash = string.Empty;
            return false;
        }

        public bool IsUrlDomainAllowed(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                foreach (string allowedMediaHostname in SceneEntityDefinition.metadata.allowedMediaHostnames)
                {
                    if (string.Equals(allowedMediaHostname, uri.Host, StringComparison.CurrentCultureIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public bool IsPortableExperience() =>
            SceneEntityDefinition.metadata.isPortableExperience;

        public bool IsSdk7() =>
            SceneEntityDefinition.metadata.runtimeVersion == "7";

        /// <summary>
        ///     Gets the specific SDK version (e.g.: "7.5.6" or "7.11.1-18575373673.commit-f7e8f37")
        ///     that is injected in Scenes Metadata during their deployment
        /// </summary>
        public string GetSDKVersion() =>
            SceneEntityDefinition.metadata.sdkVersion;

        /// <summary>
        ///     Checks if the scene's SDK version is the specified version or higher
        /// </summary>
        /// <param name="minVersion">Minimum version to check (e.g., "7.5.0")</param>
        /// <returns>True if scene SDK version >= minVersion, false if version unknown or less than minVersion</returns>
        public bool IsSDKVersionOrHigher(string minVersion)
        {
            string sdkVersion = SceneEntityDefinition.metadata.sdkVersion;

            if (string.IsNullOrEmpty(sdkVersion) || string.IsNullOrEmpty(minVersion))
                return false;

            try
            {
                Version sceneVersion = ParseSDKVersion(sdkVersion);
                Version minimumVersion = ParseSDKVersion(minVersion);

                return sceneVersion >= minimumVersion;
            }
            catch
            {
                // If version parsing fails, return false for safety
                return false;
            }
        }

        /// <summary>
        ///     Checks if the scene's SDK version matches a specific version
        /// </summary>
        public bool IsSDKVersion(string version)
        {
            string sdkVersion = SceneEntityDefinition.metadata.sdkVersion;

            if (string.IsNullOrEmpty(sdkVersion) || string.IsNullOrEmpty(version))
                return false;

            try
            {
                Version sceneVersion = ParseSDKVersion(sdkVersion);
                Version targetVersion = ParseSDKVersion(version);

                return sceneVersion == targetVersion;
            }
            catch
            {
                return false;
            }
        }

        private static Version ParseSDKVersion(string versionString)
        {
            // Handle versions like "7.5.6", "7.5", "7", or "7.11.1-18575373673.commit-f7e8f37"
            // Extract only the semantic version part (major.minor.patch) before any pre-release or build metadata

            // Use regex to extract version numbers at the start of the string
            // Pattern: one or more groups of digits separated by dots
            Match match = Regex.Match(versionString, @"^(\d+)(?:\.(\d+))?(?:\.(\d+))?");

            if (!match.Success)
                throw new FormatException($"Invalid version format: {versionString}");

            string major = match.Groups[1].Value;
            string minor = match.Groups[2].Success ? match.Groups[2].Value : "0";
            string patch = match.Groups[3].Success ? match.Groups[3].Value : "0";

            // Construct a clean version string
            string cleanVersion = $"{major}.{minor}.{patch}";

            return new Version(cleanVersion);
        }
    }
}
