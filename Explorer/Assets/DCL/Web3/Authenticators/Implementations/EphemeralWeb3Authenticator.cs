using Cysharp.Threading.Tasks;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Nethereum.Signer;
using System;
using System.Threading;
using Utility.Tasks;

namespace DCL.Web3.Authenticators
{
    public class EphemeralWeb3Authenticator : IWeb3Authenticator
    {
        private const double IDENTITY_EXPIRATION_PERIOD_FALLBACK_IN_DAYS = 30;

        private readonly IWeb3AccountFactory accountFactory;

        public EphemeralWeb3Authenticator(IWeb3AccountFactory accountFactory)
        {
            this.accountFactory = accountFactory;
        }

        public void Dispose() { }

        public UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            // The rust sign server holds a single key: the account created last is the one that signs, so the
            // ephemeral account is built after the signer has signed, leaving its key live for every request
            EthECKey ephemeralKey = EthECKey.GenerateKey()!;
            IWeb3Account signer = accountFactory.CreateRandomAccount();
            DateTime expiration = DateTime.UtcNow.AddDays(IDENTITY_EXPIRATION_PERIOD_FALLBACK_IN_DAYS);

            var ephemeralMessage = $"Decentraland Login\nEphemeral address: {ephemeralKey.GetPublicAddress()}\nExpiration: {expiration:yyyy-MM-ddTHH:mm:ss.fffZ}";
            string ephemeralSignature = signer.Sign(ephemeralMessage);

            IWeb3Account ephemeralAccount = accountFactory.CreateAccount(ephemeralKey);

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
                authChain,
                payload.Method
            ).AsUniTaskResult<IWeb3Identity>();
        }
    }
}
