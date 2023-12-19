using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3Authentication
{
    public class FakeWeb3Authenticator : IWeb3Authenticator
    {
        private readonly Web3Address customAddress;

        public IWeb3Identity? Identity { get; private set; }

        public FakeWeb3Authenticator(Web3Address customAddress)
        {
            this.customAddress = customAddress;
        }

        public void Dispose()
        {
            Identity = null;
        }

        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken cancellationToken)
        {
            var signer = new FakeWeb3Account(customAddress);
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

            Identity = new DecentralandIdentity(signer.Address, ephemeralAccount, expiration, authChain);

            return Identity;
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken) =>
            Identity = null;
    }
}
