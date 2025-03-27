using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.Threading;

namespace DCL.WebRequests
{
    public interface IWebRequestController
    {
        static readonly IWebRequestController DEFAULT = new DefaultWebRequestController(
            IWebRequestsAnalyticsContainer.DEFAULT,
            new IWeb3IdentityCache.Default(),
            new RequestHub(
                ITexturesFuse.NewDefault(),
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
        UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, CancellationToken ct);
    }
}
