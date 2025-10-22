using Cysharp.Threading.Tasks;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using System;
using System.Threading;
using Utility.Tasks;

namespace DCL.Web3.Authenticators
{
    public class RandomGeneratedWeb3Authenticator : IWeb3Authenticator
    {
        private readonly IWeb3AccountFactory accountFactory;

        public RandomGeneratedWeb3Authenticator(IWeb3AccountFactory accountFactory)
        {
            this.accountFactory = accountFactory;
        }

        public void Dispose() { }

        public UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
        {
            var signer = accountFactory.CreateRandomAccount();
            var ephemeralAccount = accountFactory.CreateRandomAccount();
            DateTime expiration = DateTime.Now.AddMinutes(600);

            var ephemeralMessage = $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";
            string ephemeralSignature = signer.Sign(ephemeralMessage);

            var authChain = AuthChain.Create();

            authChain.SetSigner(signer.Address);

            authChain.Set(new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = ephemeralMessage,
                signature = ephemeralSignature,
            });

            // To keep cohesiveness between the platform, convert the user address to lower case
            return new DecentralandIdentity(
                new Web3Address(signer),
                ephemeralAccount,
                expiration,
                authChain
            ).AsUniTaskResult<IWeb3Identity>();
        }

        public UniTask LogoutAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;
    }
}
