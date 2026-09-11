using DCL.Web3.Abstract;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Authenticators;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;

namespace DCL.Web3.Tests
{
    [TestFixture]
    public class IdentityMethodPersistenceShould
    {
        private PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer serializer = null!;
        private IWeb3AccountFactory accountFactory = null!;

        [SetUp]
        public void SetUp()
        {
            accountFactory = new Web3AccountFactory();
            serializer = new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(accountFactory);
        }

        [TestCase(LoginMethod.GUEST)]
        [TestCase(LoginMethod.EMAIL_OTP)]
        [TestCase(LoginMethod.METAMASK)]
        [TestCase(LoginMethod.GOOGLE)]
        [TestCase(LoginMethod.TOKEN_FILE)]
        public void RoundTripMethod(LoginMethod method)
        {
            // Arrange
            IWeb3Identity identity = NewIdentity(method);

            // Act
            IWeb3Identity? restored = serializer.Deserialize(serializer.Serialize(identity));

            // Assert
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.Method, Is.EqualTo(method));
        }

        [Test]
        public void PreserveAddressAcrossRoundTrip()
        {
            // Arrange
            IWeb3Identity identity = NewIdentity(LoginMethod.GUEST);

            // Act
            IWeb3Identity? restored = serializer.Deserialize(serializer.Serialize(identity));

            // Assert
            Assert.That(restored!.Address, Is.EqualTo(identity.Address));
        }

        [Test]
        public void FallBackToAnyWhenMethodIsAbsent()
        {
            // Arrange
            var json = JObject.Parse(serializer.Serialize(NewIdentity(LoginMethod.GUEST)));
            Assert.That(json.Remove("method"), Is.True, "the payload is expected to carry a method property");

            // Act
            IWeb3Identity? restored = serializer.Deserialize(json.ToString());

            // Assert
            Assert.That(restored!.Method, Is.EqualTo(LoginMethod.ANY));
        }

        [Test]
        public void FallBackToAnyWhenMethodIsUnrecognised()
        {
            // Arrange
            var json = JObject.Parse(serializer.Serialize(NewIdentity(LoginMethod.GUEST)));
            json["method"] = "SomethingElse";

            // Act
            IWeb3Identity? restored = serializer.Deserialize(json.ToString());

            // Assert
            Assert.That(restored!.Method, Is.EqualTo(LoginMethod.ANY));
        }

        private IWeb3Identity NewIdentity(LoginMethod method)
        {
            IWeb3Account signer = accountFactory.CreateRandomAccount();
            IWeb3Account ephemeral = accountFactory.CreateRandomAccount();

            DateTime expiration = DateTime.UtcNow.AddDays(7);
            var message = $"Decentraland Login\nEphemeral address: {ephemeral.Address.OriginalFormat}\nExpiration: {expiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

            AuthChain authChain = AuthChain.Create();
            authChain.SetSigner(signer.Address.ToString());

            authChain.Set(new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = message,
                signature = signer.Sign(message),
            });

            return new DecentralandIdentity(signer.Address, ephemeral, expiration, authChain, method);
        }
    }
}
