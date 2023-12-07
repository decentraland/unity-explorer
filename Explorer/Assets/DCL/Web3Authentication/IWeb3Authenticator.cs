using System.Threading;
using System.Threading.Tasks;

namespace DCL.Web3Authentication
{
    public interface IWeb3Authenticator
    {
        Task<IWeb3Identity> Login(CancellationToken cancellationToken);

        Task Logout(CancellationToken cancellationToken);
    }
}
