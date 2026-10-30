using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public interface IAccountLinkAuthenticator
    {
        UniTask SendEmailLinkOtpAsync(string email, CancellationToken ct);

        UniTask ResendEmailLinkOtpAsync(CancellationToken ct);

        UniTask<IWeb3Identity> LinkEmailAsync(string otp, CancellationToken ct);
    }
}