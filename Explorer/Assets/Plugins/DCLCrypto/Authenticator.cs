using System;
using Nethereum.Signer;

namespace DCLCrypto
{
    public static class Authenticator
    {
        public delegate string SignerFunc(string payload);
        public static string GetEphemeralMessage(string ephemeralAddress, DateTime expiration)
        {
            return $"Decentraland Login\nEphemeral address: {ephemeralAddress}\nExpiration: {expiration:s}";
        }

        public static AuthIdentity CreateRandomInsecureAuthIdentity()
        {
            Identity identity = Identity.CreateRandom();
            Identity ephemeralIdentity = Identity.CreateRandom();

            SignerFunc signerFunc = payload => identity.Sign(payload);

            return InitializeAuthChain(identity.Address(), signerFunc, ephemeralIdentity, 600);
        }

        public static AuthIdentity InitializeAuthChain(string ethAddress, SignerFunc signerFunc, Identity ephemeralIdentity, int ephemeralMinutesDuration)
        {
            var expiration = DateTime.Now.AddMinutes(ephemeralMinutesDuration);

            var ephemeralMessage = GetEphemeralMessage(ephemeralIdentity.Address(), expiration);
            var firstSignature = signerFunc(ephemeralMessage);

            var authChain = new AuthChain
            {
                new AuthLink()
                {
                    type = AuthLinkType.SIGNER,
                    payload = ethAddress,
                    signature = "",
                },
                new AuthLink()
                {
                    type = AuthLinkType.ECDSA_EPHEMERAL,
                    payload = ephemeralMessage,
                    signature = firstSignature,
                }
            };

            return new AuthIdentity(ethAddress, authChain, ephemeralIdentity, expiration);
        }
        
        public static AuthChain SignPayload(AuthIdentity authIdentity, string entityId)
        {
            var authChain = authIdentity.AuthChain.Clone(); // new list, we must not modify the AuthIdentity
            var secondSignature = authIdentity.EphemeralIdentity.Sign(entityId);
            
            authChain.Add(new AuthLink()
            {
                type = AuthLinkType.ECDSA_SIGNED_ENTITY,
                payload = entityId,
                signature = secondSignature,
            });
            
            return authChain;
        }
    }
}