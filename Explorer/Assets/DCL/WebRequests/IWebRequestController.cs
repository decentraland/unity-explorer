using Best.HTTP.Shared;
using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using Global.Dynamic.LaunchModes;
using Sentry;
using System;
using System.Text;
using System.Threading;

namespace DCL.WebRequests
{
    public interface IWebRequestController : IDisposable
    {
        private static readonly ThreadLocal<StringBuilder> BREADCRUMB_BUILDER = new (() => new StringBuilder(150));

        protected static void AddFailedBreadcrumb(in RequestEnvelope envelope)
        {
            if (!envelope.CommonArguments.URL.IsFile)
                SentrySdk.AddBreadcrumb($"Irrecoverable exception occured on executing {envelope.GetBreadcrumbString(BREADCRUMB_BUILDER.Value)}", level: BreadcrumbLevel.Info);
        }

        static readonly IWebRequestController UNITY = new DefaultWebRequestController(
            IWebRequestsAnalyticsContainer.DEFAULT,
            new IWeb3IdentityCache.Default(),
            new RequestHub(
                new DecentralandUrlsSource(DecentralandEnvironment.Zone, ILaunchMode.PLAY),
                HTTPManager.LocalCache,
                false,
                0L,
                1,
                false,
                WebRequestsMode.UNITY
            )
        );

        internal IRequestHub requestHub { get; }

        /// <summary>
        ///     Executes the <see cref="requestWrap" />, waits for the whole data received, and disposes of it
        ///     <remarks>
        ///         <list type="bullet">
        ///             <item> It will never finish for streaming requests. </item>
        ///             <item> Once launched it won't be possible to abort it outside the <see cref="ct" /> (e.g. gracefully) </item>
        ///             <item> <see cref="requestWrap" /> will be disposed by the end of execution </item>
        ///             <item> It is responsibility of the consumer to dispose of the return value</item>
        ///         </list>
        ///     </remarks>
        /// </summary>
        /// <param name="detachDownloadHandler">Detached Download Handler will outlive the response, and thus must be disposed by the caller</param>
        UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct);
    }
}
