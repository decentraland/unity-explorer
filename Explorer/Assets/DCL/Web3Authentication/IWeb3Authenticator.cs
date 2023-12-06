using System.Threading;
using System.Threading.Tasks;

namespace DCL.Web3Authentication
{
    public interface IWeb3Authenticator
    {
        Task<(IWeb3Identity identity, IWeb3Identity ephemeralIdentity)> Login(CancellationToken cancellationToken);

        Task Logout(CancellationToken cancellationToken);
    }
}
