using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3
{
    public interface IEthereumApi : IDisposable
    {
        /// <summary>
        ///     Sends a Web3 request. Defaults to SDKScene source (requires confirmation UI).
        /// </summary>
        UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct);

        /// <summary>
        ///     Sends a Web3 request with explicit source specification.
        ///     Internal source skips confirmation UI for ThirdWeb provider.
        /// </summary>
        UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct);
    }
}
