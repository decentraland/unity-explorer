using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Pool;

namespace SceneRunner.Scene
{
    public class SceneAssetBundleManifest
    {
        public static readonly SceneAssetBundleManifest NULL = new ();

        private readonly URLDomain assetBundlesBaseUrl;
        private readonly string version;
        private readonly HashSet<string> convertedFiles;


        private readonly bool ignoreConvertedFiles;

        public IReadOnlyCollection<string> ConvertedFiles => convertedFiles;

        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl, string version, IReadOnlyList<string> files)
        {
            this.assetBundlesBaseUrl = assetBundlesBaseUrl;
            this.version = version;
            convertedFiles = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
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

        public URLAddress GetAssetBundleURL(string hash) =>
            assetBundlesBaseUrl.Append(new URLPath($"{version}/{hash}"));
        
        public string GetVersion() =>
            version;
    }
}
