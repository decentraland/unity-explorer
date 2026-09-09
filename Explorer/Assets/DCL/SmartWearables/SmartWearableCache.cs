using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.SmartWearables;
using DCL.WebRequests;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using SceneRuntime.ScenePermissions;
using System;
using System.Collections.Generic;
using System.Threading;

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

        private const string NULL_DTO_WEARABLE_ID = "null-dto-wearable-id";

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        // Readers arrive from load-system flows and main-thread UI concurrently
        private readonly object gate = new ();
        private readonly Dictionary<string, CacheItem> cache = new ();
        private readonly Dictionary<string, UniTaskCompletionSource<CacheItem>> inFlight = new ();

        public SmartWearableCache(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
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
        ///     Gets the ID the cache uses to identify wearables.
        /// </summary>.
        public static string GetCacheId(IWearable wearable) =>

            // To avoid confusion, since the DTO itself has an ID that is different from the ID in the metadata
            // We want to always use this ID
            // If the DTO is null (happens in some environments), we just return a predefined ID to avoid null refs
            wearable.DTO?.Metadata.id ?? NULL_DTO_WEARABLE_ID;

        /// <summary>
        ///     Whether the wearable is a smart wearable.
        /// </summary>
        public async UniTask<bool> IsSmartAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem? item = await CacheWearableInternalAsync(wearable, ct);
            return item is { IsSmart: true };
        }

        public async UniTask<bool> RequiresAuthorizationAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem? item = await CacheWearableInternalAsync(wearable, ct);
            return item is { RequiresAuthorization: true };
        }

        public async UniTask<bool> RequiresWeb3APIAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem? item = await CacheWearableInternalAsync(wearable, ct);
            return item is { RequiresWeb3API: true };
        }

        /// <summary>
        ///     Can be used to cache the info about the wearable.
        ///     Other methods reading the cache will automatically query and cache the info of the wearable if needed.
        /// </summary>
        public async UniTask CacheWearableAsync(IWearable wearable, CancellationToken ct)
        {
            await CacheWearableInternalAsync(wearable, ct);
        }

        public bool IsCached(IWearable wearable)
        {
            lock (gate) { return cache.ContainsKey(GetCacheId(wearable)); }
        }

        public async UniTask<(ISceneContent?, SceneMetadata?)> GetCachedSceneInfoAsync(IWearable wearable, CancellationToken ct)
        {
            CacheItem? item = await CacheWearableInternalAsync(wearable, ct);
            return item == null ? (null, null) : (item.SceneContent, item.SceneMetadata);
        }

        public void Clear()
        {
            lock (gate)
            {
                cache.Clear();

                // Fetches still running are no longer registered, so they discard their result instead of refilling the cleared cache
                inFlight.Clear();
            }

            AuthorizedSmartWearables.Clear();
            RunningSmartWearables.Clear();
            KilledPortableExperiences.Clear();
        }

        /// <summary>
        ///     Returns null only when <paramref name="ct" /> was cancelled. Concurrent callers for the same wearable share one fetch,
        ///     and an entry becomes visible only once fully built. A failed fetch throws to every awaiting caller.
        /// </summary>
        private async UniTask<CacheItem?> CacheWearableInternalAsync(IWearable wearable, CancellationToken ct)
        {
            string id = GetCacheId(wearable);
            UniTaskCompletionSource<CacheItem> completion;
            var startFetch = false;

            lock (gate)
            {
                if (cache.TryGetValue(id, out CacheItem item)) return item;

                if (!inFlight.TryGetValue(id, out completion))
                {
                    completion = new UniTaskCompletionSource<CacheItem>();
                    inFlight[id] = completion;
                    startFetch = true;
                }
            }

            if (startFetch)
                FetchIntoAsync(id, wearable, completion).Forget();

            (bool cancelled, CacheItem result) = await completion.Task.AttachExternalCancellation(ct).SuppressCancellationThrow();
            return cancelled ? null : result;
        }

        private async UniTaskVoid FetchIntoAsync(string id, IWearable wearable, UniTaskCompletionSource<CacheItem> completion)
        {
            try
            {
                CacheItem item = await BuildCacheItemAsync(id, wearable);

                lock (gate)
                {
                    if (RemoveInFlight(id, completion))
                        cache[id] = item;
                }

                // Completed outside the lock so awaiters' continuations never run while it is held
                completion.TrySetResult(item);
            }
            catch (Exception e)
            {
                lock (gate) { RemoveInFlight(id, completion); }

                completion.TrySetException(e);
            }
        }

        // A fetch that outlived Clear() is no longer the registered one and must not repopulate the cache
        private bool RemoveInFlight(string id, UniTaskCompletionSource<CacheItem> completion)
        {
            if (!inFlight.TryGetValue(id, out UniTaskCompletionSource<CacheItem> current) || current != completion)
                return false;

            inFlight.Remove(id);
            return true;
        }

        private async UniTask<CacheItem> BuildCacheItemAsync(string id, IWearable wearable)
        {
            var item = new CacheItem();

            // Null DTO wearable, just consider it non-smart
            if (wearable.DTO == null) return item;

            item.IsSmart = IsSmart(wearable);
            if (!item.IsSmart) return item;

            string contentUrl = GetContentUrl(wearable);
            SmartWearableSceneContent sceneContent = SmartWearableSceneContent.Create(URLDomain.FromString(contentUrl), wearable, BodyShape.MALE);
            item.SceneContent = sceneContent;

            if (!sceneContent.TryGetContentUrl("scene.json", out URLAddress url))
            {
                // Deterministic for this wearable: cached as smart-without-metadata so it is reported once, not on every retry
                ReportHub.LogError(ReportCategory.WEARABLE, $"Could not find 'scene.json' for smart wearable '{id}'");
                return item;
            }

            var args = new CommonLoadingArguments(URLAddress.FromString(url));

            // Owned by the cache rather than by the first caller: a caller cancelling must not leave a half-built entry behind
            SceneMetadata sceneMetadata = await webRequestController.GetAsync(args, CancellationToken.None, ReportCategory.WEARABLE)
                                                                    .CreateFromJson<SceneMetadata>(WRJsonParser.Newtonsoft);

            item.SceneMetadata = sceneMetadata;
            item.IsSmart &= int.TryParse(sceneMetadata.runtimeVersion, out int version) && version >= MIN_SDK_VERSION;

            if (item.IsSmart)
            {
                List<string> permissions = sceneMetadata.requiredPermissions;

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
            if (wearable.DTO == null) return false;

            foreach (var content in wearable.DTO.content)
            {
                if (content.file.EndsWith("scene.json", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private string GetContentUrl(IWearable smartWearable)
        {
            string? dtoContentUrl = smartWearable.DTO?.ContentDownloadUrl;
            return string.IsNullOrEmpty(dtoContentUrl) ? $"{decentralandUrlsSource.Url(DecentralandUrl.PeerContent)}/" : dtoContentUrl;
        }

        private class CacheItem
        {
            public bool IsSmart;

            public ISceneContent? SceneContent;

            public SceneMetadata? SceneMetadata;

            public bool RequiresAuthorization;

            public bool RequiresWeb3API;
        }
    }
}
