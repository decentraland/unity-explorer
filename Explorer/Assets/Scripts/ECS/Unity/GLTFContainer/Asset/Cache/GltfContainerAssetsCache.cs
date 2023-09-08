using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
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

        public GltfContainerAssetsCache(int maxSize)
        {
            this.maxSize = Mathf.Min(500, maxSize);
            cache = new Dictionary<string, List<GltfContainerAsset>>(this.maxSize, this);
            var parentContainerGo = new GameObject($"POOL_CONTAINER_{nameof(GltfContainerAsset)}");
            parentContainerGo.SetActive(false);
            parentContainer = parentContainerGo.transform;
        }

        public bool TryGetOngoingRequest(string key, out UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?> ongoingRequest) =>
            throw new NotImplementedException($"{nameof(GltfContainerAssetsCache)} doesn't support web requests directly");

        public void AddOngoingRequest(string key, UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?> ongoingRequest) =>
            throw new NotImplementedException($"{nameof(GltfContainerAssetsCache)} doesn't support web requests directly");

        public void RemoveOngoingRequest(string key) =>
            throw new NotImplementedException($"{nameof(GltfContainerAssetsCache)} doesn't support web requests directly");

        public bool TryGet(in string key, out GltfContainerAsset asset)
        {
            if (cache.TryGetValue(key, out List<GltfContainerAsset> list) && list.Count > 0)
            {
                // Remove from the tail of the list
                asset = list[^1];
                list.RemoveAt(list.Count - 1);
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

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return;

            asset.Root.SetActive(false);
            asset.Root.transform.SetParent(parentContainer);
        }

        bool IEqualityComparer<string>.Equals(string x, string y) =>
            string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        int IEqualityComparer<string>.GetHashCode(string obj) =>
            obj.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}
