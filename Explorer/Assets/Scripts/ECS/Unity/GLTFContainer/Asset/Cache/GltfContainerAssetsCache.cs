using Cysharp.Threading.Tasks;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace ECS.Unity.GLTFContainer.Asset.Cache
{
    /// <summary>
    ///     Individual pool for each GltfContainer source. LRU cache
    ///     <para>Gltf Containers can't be reused</para>
    ///     TODO At the moment it is a draft and not cleaned-up at all
    /// </summary>
    public class GltfContainerAssetsCache : IStreamableCache<GltfContainerAsset, string>
    {
        private readonly Dictionary<string, List<GltfContainerAsset>> cache;
        private readonly int maxSize;

        private readonly Transform parentContainer;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<GltfContainerAsset>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public GltfContainerAssetsCache(int maxSize)
        {
            this.maxSize = Mathf.Min(500, maxSize);
            cache = new Dictionary<string, List<GltfContainerAsset>>(this.maxSize, this);
            var parentContainerGo = new GameObject($"POOL_CONTAINER_{nameof(GltfContainerAsset)}");
            parentContainerGo.SetActive(false);
            parentContainer = parentContainerGo.transform;

            OngoingRequests = new FakeDictionaryCache<UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?>>();
            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<GltfContainerAsset>>.Get();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<AssetBundleData>>);
            disposed = true;
        }

        public bool TryGet(in string key, out GltfContainerAsset asset)
        {
            if (cache.TryGetValue(key, out List<GltfContainerAsset> list) && list.Count > 0)
            {
                // Remove from the tail of the list
                asset = list[^1];
                list.RemoveAt(list.Count - 1);

                ProfilingCounters.GLTFCacheSize.Value--;
                return true;
            }

            asset = default(GltfContainerAsset);
            return false;
        }

        public void Add(in string key, GltfContainerAsset asset)
        {
            // Nothing to do, we don't reuse the existing instantiated game objects
        }

        public void Dereference(in string key, GltfContainerAsset asset)
        {
            // Return to the pool
            if (!cache.TryGetValue(key, out List<GltfContainerAsset> list))
                cache[key] = list = new List<GltfContainerAsset>(maxSize / 10);

            list.Add(asset);
            ProfilingCounters.GLTFCacheSize.Value++;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return;

            asset.Root.SetActive(false);
            asset.Root.transform.SetParent(parentContainer);
        }

        public void Unload()
        {
            var unloaded = 0;

            foreach (List<GltfContainerAsset> gltfList in cache.Values)
            foreach (GltfContainerAsset gltfAsset in gltfList)
            {
                gltfAsset.Dispose();
                unloaded++;
            }

            cache.Clear();

            ProfilingCounters.GLTFCacheSize.Value -= unloaded;
        }

        bool IEqualityComparer<string>.Equals(string x, string y) =>
            string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        int IEqualityComparer<string>.GetHashCode(string obj) =>
            obj.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}
