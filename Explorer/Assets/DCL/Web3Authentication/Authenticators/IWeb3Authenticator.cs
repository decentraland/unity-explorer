using Cysharp.Threading.Tasks;
using DCL.Web3Authentication.Identities;
using System;
using System.Threading;

namespace DCL.Web3Authentication.Authenticators
{
    public interface IWeb3Authenticator : IDisposable
    {
        UniTask<IWeb3Identity> LoginAsync(CancellationToken cancellationToken);

        UniTask LogoutAsync(CancellationToken cancellationToken);
    }
}
