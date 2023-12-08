using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3Authentication
{
    public class FakeWeb3Authenticator : IWeb3Authenticator
    {
        public IWeb3Identity? Identity { get; private set; }

        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken cancellationToken)
        {
            var signer = NethereumAccount.CreateRandom();
            var ephemeralAccount = NethereumAccount.CreateRandom();
            DateTime expiration = DateTime.Now.AddMinutes(600);

            var ephemeralMessage = $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";
            string ephemeralSignature = signer.Sign(ephemeralMessage);

            var authChain = AuthChain.Create();

            authChain.Set(AuthLinkType.SIGNER, new AuthLink
            {
                type = AuthLinkType.SIGNER,
                payload = signer.Address,
                signature = "",
            });

            authChain.Set(AuthLinkType.ECDSA_EPHEMERAL, new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = ephemeralMessage,
                signature = ephemeralSignature,
            });

            Identity = new DecentralandIdentity(ephemeralAccount, expiration, authChain);

            return Identity;
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            Identity = null;
        }
    }
}
