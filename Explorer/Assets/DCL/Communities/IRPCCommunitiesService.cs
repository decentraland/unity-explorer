using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Communities
{
    public interface IRPCCommunitiesService : IDisposable
    {
        UniTask SubscribeToConnectivityStatusAsync(CancellationToken ct);
    }
}
