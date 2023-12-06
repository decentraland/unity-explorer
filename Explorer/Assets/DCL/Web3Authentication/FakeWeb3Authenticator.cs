using System.Threading;
using System.Threading.Tasks;

namespace DCL.Web3Authentication
{
    public class FakeWeb3Authenticator : IWeb3Authenticator
    {
        public async Task<(IWeb3Identity identity, IWeb3Identity ephemeralIdentity)> Login(CancellationToken cancellationToken) =>
            (NethereumIdentity.CreateRandom(), NethereumIdentity.CreateRandom());

        public async Task Logout(CancellationToken cancellationToken) { }
    }
}
