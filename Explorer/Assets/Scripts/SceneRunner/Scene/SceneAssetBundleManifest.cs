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

        private readonly string versionHashPart;

        private readonly bool ignoreConvertedFiles;

        public IReadOnlyCollection<string> ConvertedFiles => convertedFiles;

        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl, string version, IReadOnlyList<string> files)
        {
            this.assetBundlesBaseUrl = assetBundlesBaseUrl;
            this.version = version;
            convertedFiles = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);

            versionHashPart = string.IsNullOrEmpty(version) ? ComputeVersionedHashPart(assetBundlesBaseUrl) : string.Empty;
            ignoreConvertedFiles = false;
        }

        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl)
        {
            this.assetBundlesBaseUrl = assetBundlesBaseUrl;
            versionHashPart = string.Empty;
            convertedFiles = new HashSet<string>();
            ignoreConvertedFiles = true;
        }

        /// <summary>
        ///     Null implementation with no bundles provided
        /// </summary>
        private SceneAssetBundleManifest()
        {
            assetBundlesBaseUrl = URLDomain.EMPTY;
            convertedFiles = new HashSet<string>();
        }

        private static string ComputeVersionedHashPart(in URLDomain assetBundlesUrl)
        {
            StringBuilder hashBuilder = GenericPool<StringBuilder>.Get();
            hashBuilder.Clear();

            ReadOnlySpan<char> span = assetBundlesUrl.Value.AsSpan();

            // content URL always ends with '/'
            int indexOfVersionStart;

            for (indexOfVersionStart = span.Length - 2; span[indexOfVersionStart] != '/'; indexOfVersionStart--) { }

            indexOfVersionStart++;

            if (span[indexOfVersionStart] == 'v')
                hashBuilder.Insert(0, span.Slice(indexOfVersionStart, span.Length - indexOfVersionStart - 1));

            var hash = hashBuilder.ToString();

            GenericPool<StringBuilder>.Release(hashBuilder);

            return hash;
        }

        public unsafe Hash128 ComputeHash(string hash)
        {
            Span<char> hashBuilder = stackalloc char[versionHashPart.Length + hash.Length];
            versionHashPart.AsSpan().CopyTo(hashBuilder);
            hash.AsSpan().CopyTo(hashBuilder[versionHashPart.Length..]);

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
