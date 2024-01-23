using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public interface IWeb3Authenticator : IDisposable
    {
        UniTask<IWeb3Identity> LoginAsync(CancellationToken ct);

        UniTask LogoutAsync(CancellationToken cancellationToken);
    }
}
