using System.Threading;
using System.Threading.Tasks;

namespace DCL.Web3Authentication
{
    // TODO: we could remove this class if we just cache the current identities somewhere else
    // TODO: make proper initialization method and hook it into the application's flow
    public static class Authenticator
    {
        private static readonly IWeb3Authenticator authenticator = new FakeWeb3Authenticator();
        private static IWeb3Identity identity;

        public static async Task Login(CancellationToken cancellationToken)
        {
            identity = await authenticator.Login(cancellationToken);
        }

        public static async Task Logout(CancellationToken cancellationToken)
        {
            await authenticator.Logout(cancellationToken);
            identity = null;
        }

        public static AuthChain Sign(string entityId) =>
            identity.Sign(entityId);
    }
}
