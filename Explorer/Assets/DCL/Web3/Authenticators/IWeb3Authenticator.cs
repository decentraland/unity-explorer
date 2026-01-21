using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public interface IWeb3Authenticator : IDisposable
    {
        public delegate void VerificationDelegate(int code, DateTime expiration, string requestID);

        UniTask<IWeb3Identity> LoginAsync(CancellationToken ct, VerificationDelegate? callback);

        UniTask LogoutAsync(CancellationToken cancellationToken);
    }
}
