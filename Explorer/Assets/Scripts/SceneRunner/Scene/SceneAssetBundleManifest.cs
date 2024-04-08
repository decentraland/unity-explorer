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
        internal readonly SceneAbDto dto;

        private readonly URLDomain assetBundlesBaseUrl;
        private readonly HashSet<string> convertedFiles;

        private readonly string versionHashPart;

        public IReadOnlyCollection<string> ConvertedFiles => convertedFiles;

        public SceneAssetBundleManifest(URLDomain assetBundlesBaseUrl, SceneAbDto dto)
        {
            this.assetBundlesBaseUrl = assetBundlesBaseUrl;
            this.dto = dto;
            convertedFiles = new HashSet<string>(dto.Files, StringComparer.OrdinalIgnoreCase);

            versionHashPart = string.IsNullOrEmpty(dto.Version) ? ComputeVersionedHashPart(assetBundlesBaseUrl) : dto.Version;
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
            convertedFiles.Contains(hash);

        public URLAddress GetAssetBundleURL(string hash) =>
            assetBundlesBaseUrl.Append(new URLPath($"{dto.Version}/{hash}"));

        public string GetVersion() =>
            dto.version;
    }
}
