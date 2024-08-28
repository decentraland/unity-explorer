using DCL.Web3.Abstract;
using DCL.Web3.Accounts;
using DCL.Web3.Chains;
using System;

namespace DCL.Web3.Identities
{
    public class DecentralandIdentity : IWeb3Identity
    {
        public Web3Address Address { get; }
        public DateTime Expiration { get; }
        public IWeb3Account EphemeralAccount { get; }
        public bool IsExpired => Expiration < DateTime.UtcNow;
        public AuthChain AuthChain { get; }

        public DecentralandIdentity(
            Web3Address address,
            IWeb3Account ephemeralAccount,
            DateTime expiration,
            AuthChain authChain)
        {
            AssertSigner(authChain);
            AssertEcdsaEphemeral(authChain);

            AuthChain = authChain;
            Address = address;
            EphemeralAccount = ephemeralAccount;
            Expiration = expiration;
        }

        public void Dispose()
        {
            AuthChain.Dispose();
        }

        public AuthChain Sign(string entityId)
        {
            if (Expiration < DateTime.UtcNow)
                throw new Web3IdentityException(this, $"Cannot sign, identity has expired: {Expiration:s}");

            if (string.IsNullOrEmpty(entityId))
                throw new Web3IdentityException(this, "Trying to sign an empty entity");

            var chain = AuthChain.Create();

            foreach (AuthLink link in AuthChain)
                chain.Set(link);

            chain.Set(new AuthLink
            {
                type = AuthLinkType.ECDSA_SIGNED_ENTITY,
                payload = entityId,
                signature = EphemeralAccount.Sign(entityId),
            });

            AssertSigner(chain);
            AssertEcdsaEphemeral(chain);
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
            var ecdsaExists = false;

            if (authChain.TryGet(AuthLinkType.ECDSA_EPHEMERAL, out AuthLink ecdsaEphemeral))
            {
                if (string.IsNullOrEmpty(ecdsaEphemeral.payload))
                    throw new Web3IdentityException(this, "Invalid auth chain. ECDSA_EPHEMERAL payload is empty");

                if (string.IsNullOrEmpty(ecdsaEphemeral.signature))
                    throw new Web3IdentityException(this, "Invalid auth chain. ECDSA_EPHEMERAL signature is empty");

                ecdsaExists = true;
            }

            if (authChain.TryGet(AuthLinkType.ECDSA_EIP_1654_EPHEMERAL, out AuthLink ecdsaEip1654Ephemeral))
            {
                if (string.IsNullOrEmpty(ecdsaEip1654Ephemeral.payload))
                    throw new Web3IdentityException(this, "Invalid auth chain. ECDSA_EIP_1654_EPHEMERAL payload is empty");

                if (string.IsNullOrEmpty(ecdsaEip1654Ephemeral.signature))
                    throw new Web3IdentityException(this, "Invalid auth chain. ECDSA_EIP_1654_EPHEMERAL signature is empty");

                ecdsaExists = true;
            }

            if (!ecdsaExists)
                throw new Web3IdentityException(this, "Invalid auth chain. ECDSA EPHEMERAL does not exist");
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
