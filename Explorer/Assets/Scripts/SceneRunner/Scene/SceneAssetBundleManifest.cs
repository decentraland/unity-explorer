using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    public class SceneAssetBundleManifest
    {
        //From v26 onwards, the asset bundle path contains the sceneID in the hash
        //This was done to solve cache issues
        public const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 26;
        
        public static readonly SceneAssetBundleManifest NULL = new ();

        private readonly URLDomain assetBundlesBaseUrl;
        private readonly string version;
        private readonly int versionInt;
        private readonly HashSet<string> convertedFiles;
        private readonly string sceneID;

        private readonly bool ignoreConvertedFiles;

        public IReadOnlyCollection<string> ConvertedFiles => convertedFiles;

        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl, string version, IReadOnlyList<string> files, string sceneID)
        {
            this.assetBundlesBaseUrl = assetBundlesBaseUrl;
            this.version = version;
            versionInt = int.Parse(version.AsSpan().Slice(1));
            convertedFiles = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            this.sceneID = sceneID;
            ignoreConvertedFiles = false;
        }

        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl)
        {
            this.assetBundlesBaseUrl = assetBundlesBaseUrl;
            convertedFiles = new HashSet<string>();
            ignoreConvertedFiles = true;
            version = "";
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
            Span<char> hashBuilder = stackalloc char[version.Length + hash.Length];
            version.AsSpan().CopyTo(hashBuilder);
            hash.AsSpan().CopyTo(hashBuilder[version.Length..]);

            fixed (char* ptr = hashBuilder) { return Hash128.Compute(ptr, (uint)(sizeof(char) * hashBuilder.Length)); }
        }

        public bool Contains(string hash) =>
            ignoreConvertedFiles || convertedFiles.Contains(hash);

        public bool TryGet(string hash, out string convertedFile) => convertedFiles.TryGetValue(hash, out convertedFile);

        public URLAddress GetAssetBundleURL(string hash)
        {
            if (versionInt >= ASSET_BUNDLE_VERSION_REQUIRES_HASH)
                return assetBundlesBaseUrl.Append(new URLPath($"{version}/{sceneID}/{hash}"));
            
            return assetBundlesBaseUrl.Append(new URLPath($"{version}/{hash}"));
        }

        public string GetVersion() =>
            version;
    }
}
