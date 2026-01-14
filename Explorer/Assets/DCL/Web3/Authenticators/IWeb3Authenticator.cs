using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public enum LoginMethod
    {
        ANY = 0,
        METAMASK = 1,
        GOOGLE = 2,
        EMAIL_OTP = 3,
    }

    public interface IWeb3Authenticator : IDisposable
    {
        UniTask<IWeb3Identity> LoginAsync(LoginMethod loginMethod, CancellationToken ct);

        UniTask<IWeb3Identity> LoginPayloadedAsync<TPayload>(LoginMethod method, TPayload payload, CancellationToken ct);

        UniTask LogoutAsync(CancellationToken ct);
    }
}
