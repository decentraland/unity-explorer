using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace SceneRunner.Scene
{
    public class SceneAssetBundleManifest
    {
        //From v25 onwards, the asset bundle path contains the sceneID in the hash
        //This was done to solve cache issues
        public const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 25;
        public static readonly SceneAssetBundleManifest NULL = new ();

        private readonly string version;
        private readonly HashSet<string> convertedFiles;
        private readonly string buildDate;
        private readonly bool ignoreConvertedFiles;

        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl, string version, IReadOnlyList<string> files, string sceneID, string buildDate)
        {
            this.version = version;
            convertedFiles = new HashSet<string>(files, new UrlHashComparer());
            this.buildDate = buildDate;
            ignoreConvertedFiles = false;
        }

        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl)
        {
            convertedFiles = new HashSet<string>();
            ignoreConvertedFiles = true;
            version = "";
            buildDate = "";
        }

        /// <summary>
        ///     Null implementation with no bundles provided
        /// </summary>
        private SceneAssetBundleManifest()
        {
            convertedFiles = new HashSet<string>();
        }

        public unsafe Hash128 ComputeHash(string hash)
        {
            Span<char> hashBuilder = stackalloc char[buildDate.Length + hash.Length];
            buildDate.AsSpan().CopyTo(hashBuilder);
            hash.AsSpan().CopyTo(hashBuilder[buildDate.Length..]);

            fixed (char* ptr = hashBuilder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * hashBuilder.Length)); }
        }

        public bool Contains(string hash) =>
            ignoreConvertedFiles || convertedFiles.Contains(hash);

        public bool TryGet(string hash, out string convertedFile) =>
            convertedFiles.TryGetValue(hash, out convertedFile);

        public string GetVersion() =>
            version;

    }
}
