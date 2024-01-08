using Cysharp.Threading.Tasks;
using DCL.Web3Authentication.Accounts;
using DCL.Web3Authentication.Chains;
using DCL.Web3Authentication.Identities;
using System;
using System.Threading;

namespace DCL.Web3Authentication.Signatures
{
    public class RandomGeneratedWeb3Authenticator : IWeb3Authenticator
    {
        public void Dispose()
        {
        }

        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
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

            // To keep cohesiveness between the platform, convert the user address to lower case
            return new DecentralandIdentity(new Web3Address(signer.Address.ToString().ToLower()), ephemeralAccount, expiration, authChain);
        }

        public UniTask LogoutAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;
    }
}
