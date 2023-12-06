using System;

namespace DCL.Web3Authentication
{
    public class DecentralandEntityPayloadSigningProtocol : IWeb3EntityPayloadSigningProtocol
    {
        private readonly IWeb3Identity identity;
        private readonly IWeb3Identity ephemeralIdentity;
        private readonly DateTime expiration;

        public DecentralandEntityPayloadSigningProtocol(
            IWeb3Identity identity,
            IWeb3Identity ephemeralIdentity,
            DateTime expiration)
        {
            this.identity = identity;
            this.ephemeralIdentity = ephemeralIdentity;
            this.expiration = expiration;
        }

        public AuthChain Sign(string entityId)
        {
            var ephemeralMessage = $"Decentraland Login\nEphemeral address: {ephemeralIdentity.Address}\nExpiration: {expiration:s}";
            string ephemeralSignature = identity.Sign(ephemeralMessage);

            return new AuthChain
            {
                new ()
                {
                    type = AuthLinkType.SIGNER,
                    payload = identity.Address,
                    signature = "",
                },
                new ()
                {
                    type = AuthLinkType.ECDSA_EPHEMERAL,
                    payload = ephemeralMessage,
                    signature = ephemeralSignature,
                },
                new ()
                {
                    type = AuthLinkType.ECDSA_SIGNED_ENTITY,
                    payload = entityId,
                    signature = ephemeralIdentity.Sign(entityId),
                },
            };
        }
    }
}
