using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public enum LoginMethod
    {
        ANY = 0,
        EMAIL_OTP = 1,

        METAMASK = 2,
        GOOGLE = 3,

        DISCORD = 4,
        APPLE = 5,
        X = 6,
        FORTMATIC = 7,
        COINBASE = 8,
        WALLETCONNECT = 9,
    }

    public interface IWeb3Authenticator : IDisposable
    {
        UniTask<IWeb3Identity> LoginAsync(LoginMethod loginMethod, CancellationToken ct);

        UniTask<IWeb3Identity> LoginPayloadedAsync<TPayload>(LoginMethod method, TPayload payload, CancellationToken ct);

        UniTask LogoutAsync(CancellationToken ct);
    }
}
