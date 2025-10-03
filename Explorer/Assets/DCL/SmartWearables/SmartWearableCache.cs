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
using SceneRuntime.ScenePermissions;
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

        public bool CurrentSceneAllowsSmartWearables { get; set; }

        /// <summary>
        ///     Keeps track of wearables that were authorized during the current session.
        ///     We won't ask the user again for authorization of those wearables.
        /// </summary>
        public HashSet<string> AuthorizedSmartWearables { get; } = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Contains the IDs of Smart Wearables equipped and that are currently running.
        /// </summary>
        public HashSet<string> RunningSmartWearables { get; } = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Contains the IDs of Smart Wearables that were manually killed by the user.
        /// </summary>
        public HashSet<string> KilledPortableExperiences { get; } = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Whether the wearable is a smart wearable.
        /// </summary>
        public async Task<bool> IsSmartAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem item = await CacheWearableInternalAsync(wearable, ct);
            return !ct.IsCancellationRequested && item.IsSmart;
        }

        public async UniTask<bool> RequiresAuthorizationAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem item = await CacheWearableInternalAsync(wearable, ct);
            return !ct.IsCancellationRequested && item.RequiresAuthorization;
        }

        public async UniTask<bool> RequiresWeb3APIAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem item = await CacheWearableInternalAsync(wearable, ct);
            return !ct.IsCancellationRequested && item.RequiresWeb3API;
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
            cache.ContainsKey(wearable.DTO.Metadata.id);

        public async UniTask<(ISceneContent, SceneMetadata)> GetCachedSceneInfoAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem item = await CacheWearableInternalAsync(wearable, ct);
            return ct.IsCancellationRequested ? (null, null) : (item.SceneContent, item.SceneMetadata);
        }

        private async UniTask<CacheItem> CacheWearableInternalAsync(IWearable wearable, CancellationToken ct)
        {
            if (cache.TryGetValue(wearable.DTO.Metadata.id, out CacheItem item)) return item;

            item = new CacheItem();
            cache.Add(wearable.DTO.Metadata.id, item);

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

            if (item.IsSmart)
            {
                List<string> permissions = item.SceneMetadata.requiredPermissions;

                item.RequiresWeb3API = permissions.Contains(ScenePermissionNames.USE_WEB3_API);
                item.RequiresAuthorization = item.RequiresWeb3API ||
                                             permissions.Contains(ScenePermissionNames.OPEN_EXTERNAL_LINK) ||
                                             permissions.Contains(ScenePermissionNames.USE_WEBSOCKET) ||
                                             permissions.Contains(ScenePermissionNames.USE_FETCH);
            }

            return item;
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

            public bool RequiresAuthorization;

            public bool RequiresWeb3API;
        }
    }
}
