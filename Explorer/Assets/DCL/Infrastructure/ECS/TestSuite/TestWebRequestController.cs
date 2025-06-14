using Best.HTTP.Caching;
using Best.HTTP.Shared;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.HTTP2;
using DCL.WebRequests.RequestsHub;
using NSubstitute;
using System;
using System.IO;
using UnityEngine;

namespace ECS.TestSuite
{
    public class TestWebRequestController
    {
        /// <summary>
        ///     <see cref="RestoreCache" /> must be called on "TearDown"
        /// </summary>
        public static HTTPCache InitializeCache()
        {
            // Redirect tests cache to another folder
            HTTPManager.RootSaveFolderProvider = () => Path.Combine(Application.persistentDataPath, "TestCache");

            // Initialize cache with some big values (e.g. 10 GB) so it does not shrink the real cache immediately
            var cache = new HTTPCache(new HTTPCacheOptions(TimeSpan.MaxValue, 10UL * 1024UL * 1024UL * 1024UL));
            HTTPManager.LocalCache = cache;

            return cache;
        }

        public static void RestoreCache()
        {
            HTTPManager.LocalCache = null;
            HTTPManager.RootSaveFolderProvider = null;
        }

        /// <summary>
        ///     <see cref="RestoreCache" /> must be called on TearDown
        /// </summary>
        public static IWebRequestController Create(WebRequestsMode mode, HTTPCache? cache = null, long chunkSize = long.MaxValue)
        {
            cache ??= InitializeCache();

            var hub = new RequestHub(Substitute.For<IDecentralandUrlsSource>(), cache, mode != WebRequestsMode.UNITY, chunkSize, false, mode);
            IWebRequestsAnalyticsContainer? analyticsContainer = Substitute.For<IWebRequestsAnalyticsContainer>();
            IWeb3IdentityCache? identityCache = Substitute.For<IWeb3IdentityCache>();

            return new DisposeRequestWrap(new RedirectWebRequestController(mode,
                new DefaultWebRequestController(analyticsContainer, identityCache, hub),
                new Http2WebRequestController(analyticsContainer, identityCache, hub),
                new YetAnotherWebRequestController(analyticsContainer, identityCache, hub),
                hub));
        }
    }
}
