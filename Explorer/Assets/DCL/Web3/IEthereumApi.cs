using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3
{
    public interface IEthereumApi : IDisposable
    {
        /// <summary>
        ///     Sends a Web3 request with explicit source specification.
        /// </summary>
        UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct);
    }
}
