using System;

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
            AssertSigner(authChain);
            AssertEcdsaEphemeral(authChain);

            this.authChain = authChain;
            EphemeralAccount = ephemeralAccount;
            Expiration = expiration;
        }

        public AuthChain Sign(string entityId)
        {
            if (Expiration < DateTime.UtcNow)
                throw new Web3IdentityException(this, $"Cannot sign, identity has expired: {Expiration:s}");

            if (string.IsNullOrEmpty(entityId))
                throw new Web3IdentityException(this, "Trying to sign an empty entity");

            var chain = AuthChain.Create();

            chain.Set(AuthLinkType.SIGNER, authChain.Get((int)AuthLinkType.SIGNER));
            chain.Set(AuthLinkType.ECDSA_EPHEMERAL, authChain.Get(AuthLinkType.ECDSA_EPHEMERAL));

            chain.Set(AuthLinkType.ECDSA_SIGNED_ENTITY, new AuthLink
            {
                type = AuthLinkType.ECDSA_SIGNED_ENTITY,
                payload = entityId,
                signature = EphemeralAccount.Sign(entityId),
            });

            AssertSigner(authChain);
            AssertEcdsaEphemeral(authChain);
            AssertEcdsaSignedEntity(chain);

            return chain;
        }

        private void AssertSigner(AuthChain authChain)
        {
            AuthLink signer = authChain.Get(AuthLinkType.SIGNER);

            if (string.IsNullOrEmpty(signer.payload))
                throw new Web3IdentityException(this, "Invalid auth chain. SIGNER payload is empty");

            if (!string.IsNullOrEmpty(signer.signature))
                throw new Web3IdentityException(this, "Invalid auth chain. SIGNER signature should be empty");
        }

        private void AssertEcdsaEphemeral(AuthChain authChain)
        {
            AuthLink ecdsaEphemeral = authChain.Get(AuthLinkType.ECDSA_EPHEMERAL);

            if (string.IsNullOrEmpty(ecdsaEphemeral.payload))
                throw new Web3IdentityException(this, "Invalid auth chain. ECDSA_EPHEMERAL payload is empty");

            if (string.IsNullOrEmpty(ecdsaEphemeral.signature))
                throw new Web3IdentityException(this, "Invalid auth chain. ECDSA_EPHEMERAL signature is empty");
        }

        private void AssertEcdsaSignedEntity(AuthChain authChain)
        {
            AuthLink ecdsaSignedEntity = authChain.Get(AuthLinkType.ECDSA_SIGNED_ENTITY);

            if (string.IsNullOrEmpty(ecdsaSignedEntity.payload))
                throw new Web3IdentityException(this, "Invalid auth chain. ECDSA_SIGNED_ENTITY payload is empty");

            if (string.IsNullOrEmpty(ecdsaSignedEntity.signature))
                throw new Web3IdentityException(this, "Invalid auth chain. ECDSA_SIGNED_ENTITY signature is empty");
        }
    }
}
