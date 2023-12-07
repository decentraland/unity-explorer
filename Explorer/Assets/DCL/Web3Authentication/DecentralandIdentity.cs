using System;
using Utility.ThreadSafePool;

namespace DCL.Web3Authentication
{
    public class DecentralandIdentity : IWeb3Identity
    {
        private readonly AuthChain authChain;

        public DateTime Expiration { get; }
        public IWeb3Account EphemeralAccount { get; }

        public DecentralandIdentity(
            IWeb3Account ephemeralAccount,
            DateTime expiration,
            AuthChain authChain)
        {
            this.authChain = authChain;
            EphemeralAccount = ephemeralAccount;
            Expiration = expiration;
        }

        public AuthChain Sign(string entityId)
        {
            var chain = AuthChain.Create();

            chain.Set(AuthLinkType.SIGNER, chain.Get((int)AuthLinkType.SIGNER));
            chain.Set(AuthLinkType.ECDSA_EPHEMERAL, chain.Get(AuthLinkType.ECDSA_EPHEMERAL));

            chain.Set(AuthLinkType.ECDSA_SIGNED_ENTITY, new AuthLink
            {
                type = AuthLinkType.ECDSA_SIGNED_ENTITY,
                payload = entityId,
                signature = EphemeralAccount.Sign(entityId),
            });

            return chain;
        }
    }
}
