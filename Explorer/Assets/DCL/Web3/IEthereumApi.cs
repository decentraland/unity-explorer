using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3
{
    public interface IEthereumApi : IDisposable
    {
        UniTask<T> SendAsync<T>(EthApiRequest request, CancellationToken ct);
    }
}
