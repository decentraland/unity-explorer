using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Web3Authentication
{
    public interface IWeb3IdentityProvider
    {
        IWeb3Identity? Identity { get; }

        UniTask<IWeb3Identity> GetOwnIdentityAsync(CancellationToken ct);
    }
}
