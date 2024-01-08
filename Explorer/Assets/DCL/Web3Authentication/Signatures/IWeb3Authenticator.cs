using Cysharp.Threading.Tasks;
using DCL.Web3Authentication.Identities;
using System;
using System.Threading;

namespace DCL.Web3Authentication.Signatures
{
    public interface IWeb3Authenticator : IDisposable
    {
        UniTask<IWeb3Identity> LoginAsync(CancellationToken ct);

        UniTask LogoutAsync(CancellationToken cancellationToken);
    }
}
