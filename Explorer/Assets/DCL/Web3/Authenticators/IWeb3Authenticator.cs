using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public interface IWeb3Authenticator : IDisposable
    {
        public UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct);

        UniTask LogoutAsync(CancellationToken ct);
    }

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

    public readonly struct LoginPayload
    {
        public LoginMethod Method { get; }
        public string? Email { get; }

        private LoginPayload(LoginMethod method, string? email = null)
        {
            Method = method;
            Email = email;
        }

        public static LoginPayload ForOtpFlow(string email) =>
            new (LoginMethod.EMAIL_OTP, email);

        public static LoginPayload ForDappFlow(LoginMethod method) =>
            new (method);
    }
}
