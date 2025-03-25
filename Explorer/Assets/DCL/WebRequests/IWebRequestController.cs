using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.Collections.Generic;
using System.Threading;

namespace DCL.WebRequests
{
    public interface IWebRequestController
    {
        static readonly IWebRequestController DEFAULT = new WebRequestController(
            IWebRequestsAnalyticsContainer.DEFAULT,
            new IWeb3IdentityCache.Default(),
            new RequestHub(
                ITexturesFuse.NewDefault(),
                false
            )
        );

        public static readonly ISet<long> IGNORE_NOT_FOUND = new HashSet<long> { WebRequestUtils.NOT_FOUND };

        internal IRequestHub requestHub { get; }

        /// <summary>
        ///     Executes the <see cref="requestWrap" />, waits for the whole data received, and disposes of it
        ///     <remarks>
        ///         It will never finish for streaming requests. <br />
        ///         Once launched it won't be possible to abort it outside the <see cref="ct" /> (e.g. gracefully).
        ///     </remarks>
        /// </summary>
        UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, CancellationToken ct);
    }
}
