using System;

namespace DCL.Web3Authentication
{
    // TODO: we could remove this class if we just cache the current identities somewhere else
    public static class Authenticator
    {
        public delegate string SignerFunc(string payload);

        public static string GetEphemeralMessage(string ephemeralAddress, DateTime expiration) =>
            $"Decentraland Login\nEphemeral address: {ephemeralAddress}\nExpiration: {expiration:s}";

        public static AuthIdentity CreateRandomInsecureAuthIdentity()
        {
            var identity = NethereumIdentity.CreateRandom();
            var ephemeralIdentity = NethereumIdentity.CreateRandom();

            SignerFunc signerFunc = payload => identity.Sign(payload);

            return InitializeAuthChain(identity.Address, signerFunc, ephemeralIdentity, 600);
        }

        public static AuthIdentity InitializeAuthChain(string ethAddress, SignerFunc signerFunc, IWeb3Identity ephemeralIdentity, int ephemeralMinutesDuration)
        {
            DateTime expiration = DateTime.Now.AddMinutes(ephemeralMinutesDuration);

            string ephemeralMessage = GetEphemeralMessage(ephemeralIdentity.Address, expiration);
            string firstSignature = signerFunc(ephemeralMessage);

            var authChain = new AuthChain
            {
                new ()
                {
                    type = AuthLinkType.SIGNER,
                    payload = ethAddress,
                    signature = "",
                },
                new ()
                {
                    type = AuthLinkType.ECDSA_EPHEMERAL,
                    payload = ephemeralMessage,
                    signature = firstSignature,
                },
            };

            return new AuthIdentity(ethAddress, authChain, ephemeralIdentity, expiration);
        }

        public static AuthChain SignPayload(AuthIdentity authIdentity, string entityId)
        {
            AuthChain authChain = authIdentity.AuthChain.Clone(); // new list, we must not modify the AuthIdentity
            string secondSignature = authIdentity.EphemeralIdentity.Sign(entityId);

            authChain.Add(new AuthLink
            {
                type = AuthLinkType.ECDSA_SIGNED_ENTITY,
                payload = entityId,
                signature = secondSignature,
            });

            return authChain;
        }

        // TODO: make proper initialization method and hook it into the application's flow
        private static readonly IWeb3EntityPayloadSigningProtocol entityPayloadSigningProtocol =
            new DecentralandEntityPayloadSigningProtocol(NethereumIdentity.CreateRandom(), NethereumIdentity.CreateRandom(),
                DateTime.Now.AddMinutes(600));

        public static AuthChain SignPayload(string entityId) =>
            entityPayloadSigningProtocol.Sign(entityId);
    }
}
