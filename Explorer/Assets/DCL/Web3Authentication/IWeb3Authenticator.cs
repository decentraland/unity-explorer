using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Web3Authentication
{
    public interface IWeb3Authenticator
    {
        IWeb3Identity Identity { get; }

        UniTask<IWeb3Identity> LoginAsync(CancellationToken cancellationToken);

        UniTask LogoutAsync(CancellationToken cancellationToken);
    }
}
