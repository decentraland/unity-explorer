using Best.HTTP.Shared;
using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using Global.Dynamic.LaunchModes;
using System.Threading;

namespace DCL.WebRequests
{
    public interface IWebRequestController
    {
        static readonly IWebRequestController UNITY = new DefaultWebRequestController(
            IWebRequestsAnalyticsContainer.DEFAULT,
            new IWeb3IdentityCache.Default(),
            new RequestHub(
                new DecentralandUrlsSource(DecentralandEnvironment.Zone, ILaunchMode.PLAY),
                HTTPManager.LocalCache,
                false,
                false
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

        /// <summary>
        ///     Receives a chunk of the response. if <see cref="PartialDownloadArguments.Stream" /> is provided it will be used to continue the partial request
        /// </summary>
        /// <param name="commonArguments"></param>
        /// <param name="reportData"></param>
        /// <param name="partialArgs">If provided will continue retrieving chunks from where it stopped before</param>
        /// <param name="ct"></param>
        /// <param name="headersInfo">"Content-Range" header will be supplied automatically based on <see cref="partialArgs" /></param>
        /// <returns>The partial stream will be returned only if the request is fully successful. If any error occurs during the process the stream will be disposed of. But if it is returned it's the responsibility of the consumer to dispose of it properly</returns>
        /// <exception cref="System.Exception">Will be thrown if the partial stream could not finalize</exception>
        UniTask<PartialDownloadStream> GetPartialAsync(CommonArguments commonArguments, ReportData reportData, PartialDownloadArguments partialArgs, CancellationToken ct, WebRequestHeadersInfo? headersInfo = null);
    }
}
