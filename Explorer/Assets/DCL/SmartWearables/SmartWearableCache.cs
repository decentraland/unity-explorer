using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.SmartWearables;
using DCL.WebRequests;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Runtime.Wearables
{
    /// <summary>
    /// Stores data about Smart Wearables.
    ///
    /// This is most useful because we frequently need to access the metadata of the scene associated with the wearable.
    /// To retrieve that data, we need to send a web request. The main purpose of this cache is to store that info so
    /// that the request is sent only once and the data is stored in memory and easily accessible.
    /// </summary>
    public class SmartWearableCache
    {
        private const int MIN_SDK_VERSION = 7;

        private readonly IWebRequestController webRequestController;

        private readonly Dictionary<string, CacheItem> cache = new ();

        public SmartWearableCache(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        /// <summary>
        ///     Whether the wearable is a smart wearable.
        /// </summary>
        public async Task<bool> IsSmartAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem item = await CacheWearableInternalAsync(wearable, ct);
            if (ct.IsCancellationRequested) return false;

            return item.IsSmart;
        }

        private bool IsSmart(IWearable wearable)
        {
            foreach (var content in wearable.DTO.content)
            {
                if (content.file.EndsWith("scene.json", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        ///     Can be used to cache the info about the wearable.
        ///     Other methods reading the cache will automatically query and cache the info of the wearable if needed.
        /// </summary>
        public async UniTask CacheWearableAsync(IWearable wearable, CancellationToken ct)
        {
            await CacheWearableInternalAsync(wearable, ct);
        }

        public bool IsCached(IWearable wearable) =>
            cache.ContainsKey(wearable.DTO.id!);

        public async UniTask<(ISceneContent, SceneMetadata)> GetCachedSceneInfoAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem item = await CacheWearableInternalAsync(wearable, ct);
            if (ct.IsCancellationRequested) return (null, null);

            return (item.SceneContent, item.SceneMetadata);
        }

        private async UniTask<CacheItem> CacheWearableInternalAsync(IWearable wearable, CancellationToken ct)
        {
            if (cache.TryGetValue(wearable.DTO.id!, out CacheItem item)) return item;

            item = new CacheItem();
            cache.Add(wearable.DTO.id!, item);

            item.IsSmart = IsSmart(wearable);
            if (!item.IsSmart) return item;

            string contentUrl = GetContentUrl(wearable);
            item.SceneContent = SmartWearableSceneContent.Create(URLDomain.FromString(contentUrl), wearable, BodyShape.MALE);

            if (!item.SceneContent.TryGetContentUrl("scene.json", out URLAddress url))
            {
                ReportHub.LogError(ReportCategory.WEARABLE, "Could not find 'scene.json'");
                return item;
            }

            var args = new CommonLoadingArguments(URLAddress.FromString(url));
            item.SceneMetadata = await webRequestController.GetAsync(args, ct, ReportCategory.WEARABLE)
                                                           .CreateFromJson<SceneMetadata>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);
            if (ct.IsCancellationRequested) return null;

            item.IsSmart &= int.TryParse(item.SceneMetadata.runtimeVersion, out int version) && version >= MIN_SDK_VERSION;

            return item;
        }

        private string GetContentUrl(IWearable smartWearable)
        {
            const string DEFAULT_CONTENT_URL = "https://peer.decentraland.org/content/contents/";
            string? dtoContentUrl = smartWearable.DTO.ContentDownloadUrl;
            return string.IsNullOrEmpty(dtoContentUrl) ? DEFAULT_CONTENT_URL : dtoContentUrl;
        }

        private class CacheItem
        {
            public bool IsSmart;

            public ISceneContent SceneContent;

            public SceneMetadata SceneMetadata;
        }
    }
}
