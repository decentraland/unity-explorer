using Cysharp.Threading.Tasks;
using DCL.Web3.Abstract;
using DCL.Web3.Accounts;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Nethereum.Signer;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Creates Simple Auth Chain without an ephemeral address
    /// </summary>
    public class PrivateKeyAuthenticator : IWeb3Authenticator
    {
        public class SimpleIdentity : IWeb3Identity
        {
            public SimpleIdentity(Web3Address address, IWeb3Account account, AuthChain authChain)
            {
                this.EphemeralAccount = account;
                AuthChain = authChain;
                Address = address;
            }

            public void Dispose() { }

            public Web3Address Address { get; }

            public DateTime Expiration => DateTime.MaxValue;

            public IWeb3Account EphemeralAccount { get; }

            public bool IsExpired => false;

            public AuthChain AuthChain { get; }

            public AuthChain Sign(string entityId)
            {
                var chain = AuthChain.Create();

                foreach (AuthLink link in AuthChain)
                    chain.Set(link);

                chain.Set(new AuthLink
                {
                    type = AuthLinkType.ECDSA_SIGNED_ENTITY,
                    payload = entityId,
                    signature = EphemeralAccount.Sign(entityId),
                });

                return chain;
            }
        }

        private readonly string privateKey;

        public PrivateKeyAuthenticator(string privateKey)
        {
            this.privateKey = privateKey;
        }

        public void Dispose() { }

        public UniTask<IWeb3Identity> LoginAsync(CancellationToken ct) =>
            UniTask.FromResult(Login(privateKey));

        public static IWeb3Identity Login(string privateKey)
        {
            var ethKey = new EthECKey(privateKey);
            var nethAccount = NethereumAccount.CreateForVerifyOnly(ethKey);

            //DateTime expiration = DateTime.Now.AddMinutes(600);

            //var ephemeralAccount = NethereumAccount.CreateForVerifyOnly(EthECKey.GenerateKey());

            AuthChain authChain = CreateSimpleAuthChain(nethAccount);

            var identity = new SimpleIdentity(new Web3Address(nethAccount), nethAccount, authChain);
            return identity;
        }

        private static AuthChain CreateSimpleAuthChain(IWeb3Account account)
        {
            var authChain = AuthChain.Create();
            authChain.SetSigner(account.Address);
            return authChain;
        }

        private static AuthChain CreateAuthChain(IWeb3Account account, IWeb3Account ephemeralAccount, DateTime expiration)
        {
            string ephemeralMessage = $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";

            // Use an ephemeral account instead?
            string ephemeralSignature = ephemeralAccount.Sign(ephemeralMessage);

            var authChain = AuthChain.Create();

            authChain.SetSigner(account.Address);

            authChain.Set(new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = ephemeralMessage,
                signature = ephemeralSignature,
            });

            return authChain;
        }

        public UniTask LogoutAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;
    }
}
