using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Web3Authentication
{
    // TODO: we could remove this class if we just cache the current identities somewhere else
    // TODO: make proper initialization method and hook it into the application's flow
    public static class Authenticator
    {
        private static readonly IWeb3Authenticator authenticator = new FakeWeb3Authenticator();
        private static IWeb3Identity identity;

        public static async UniTask<IWeb3Identity> LoginAsync(CancellationToken cancellationToken)
        {
            identity = await authenticator.LoginAsync(cancellationToken);
            return identity;
        }

        public static async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            await authenticator.LogoutAsync(cancellationToken);
            identity = null;
        }

        public static AuthChain Sign(string entityId) =>
            identity.Sign(entityId);
    }
}
