using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Web3Authentication
{
    public class FakeWeb3Authenticator : IWeb3Authenticator
    {
        public async Task<IWeb3Identity> Login(CancellationToken cancellationToken)
        {
            var signer = NethereumAccount.CreateRandom();
            var ephemeralAccount = NethereumAccount.CreateRandom();
            DateTime expiration = DateTime.Now.AddMinutes(600);

            var ephemeralMessage = $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";
            string ephemeralSignature = signer.Sign(ephemeralMessage);

            var authChain = new AuthChain
            {
                new ()
                {
                    type = AuthLinkType.SIGNER,
                    payload = signer.Address,
                    signature = "",
                },
                new ()
                {
                    type = AuthLinkType.ECDSA_EPHEMERAL,
                    payload = ephemeralMessage,
                    signature = ephemeralSignature,
                },
            };

            return new DecentralandIdentity(ephemeralAccount, expiration, authChain);
        }

        public async Task Logout(CancellationToken cancellationToken) { }
    }
}
