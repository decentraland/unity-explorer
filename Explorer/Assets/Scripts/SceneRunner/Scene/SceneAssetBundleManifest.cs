using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace SceneRunner.Scene
{
    public class SceneAssetBundleManifest
    {
        public static readonly SceneAssetBundleManifest NULL = new ();

        private readonly URLDomain assetBundlesBaseUrl;
        private readonly string version;
        private readonly HashSet<string> convertedFiles;
        private readonly string sceneID;
        private readonly string buildDate;
        private readonly bool ignoreConvertedFiles;

        //From v25 onwards, the asset bundle path contains the sceneID in the hash
        //This was done to solve cache issues
        public const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 25;
        private bool hasSceneIDInPath;


        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl, string version, IReadOnlyList<string> files, string sceneID, string buildDate)
        {
            this.assetBundlesBaseUrl = assetBundlesBaseUrl;
            this.version = version;
            hasSceneIDInPath = int.Parse(version.AsSpan().Slice(1)) >= 25;
            convertedFiles = new HashSet<string>(files, new UrlHashComparer());
            this.sceneID = sceneID;
            this.buildDate = buildDate;
            ignoreConvertedFiles = false;
        }

        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl)
        {
            this.assetBundlesBaseUrl = assetBundlesBaseUrl;
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
            assetBundlesBaseUrl = URLDomain.EMPTY;
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

        public bool TryGet(string hash, out string convertedFile) => convertedFiles.TryGetValue(hash, out convertedFile);

        public URLAddress GetAssetBundleURL(string hash)
        {
            if (hasSceneIDInPath)
                return assetBundlesBaseUrl.Append(new URLPath($"{version}/{sceneID}/{hash}"));

            return assetBundlesBaseUrl.Append(new URLPath($"{version}/{hash}"));
        }

        public string GetVersion() =>
            version;

        //Used for the OngoingRequests cache. We need to avoid version and sceneID in this URL to able to reuse assets.
        //The first loaded hash will be the one used for all the other requests
        public URLAddress GetCacheableURL(string hash)
        {
            return assetBundlesBaseUrl.Append(new URLPath(hash));
        }
    }
}
