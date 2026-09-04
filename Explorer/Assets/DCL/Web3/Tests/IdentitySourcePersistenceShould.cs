using DCL.Web3.Abstract;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;

namespace DCL.Web3.Tests
{
    [TestFixture]
    public class IdentitySourcePersistenceShould
    {
        private PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer serializer = null!;
        private IWeb3AccountFactory accountFactory = null!;

        [SetUp]
        public void SetUp()
        {
            accountFactory = new Web3AccountFactory();
            serializer = new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(accountFactory);
        }

        [TestCase(IWeb3Identity.Web3IdentitySource.Guest)]
        [TestCase(IWeb3Identity.Web3IdentitySource.OTP)]
        [TestCase(IWeb3Identity.Web3IdentitySource.Dapp)]
        [TestCase(IWeb3Identity.Web3IdentitySource.Deeplink)]
        [TestCase(IWeb3Identity.Web3IdentitySource.TokenFile)]
        public void RoundTripSource(IWeb3Identity.Web3IdentitySource source)
        {
            // Arrange
            IWeb3Identity identity = NewIdentity(source);

            // Act
            IWeb3Identity? restored = serializer.Deserialize(serializer.Serialize(identity));

            // Assert
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.Source, Is.EqualTo(source));
        }

        [Test]
        public void PreserveAddressAcrossRoundTrip()
        {
            // Arrange
            IWeb3Identity identity = NewIdentity(IWeb3Identity.Web3IdentitySource.Guest);

            // Act
            IWeb3Identity? restored = serializer.Deserialize(serializer.Serialize(identity));

            // Assert
            Assert.That(restored!.Address, Is.EqualTo(identity.Address));
        }

        [Test]
        public void FallBackToCachedWhenSourceIsAbsent()
        {
            // Arrange
            var json = JObject.Parse(serializer.Serialize(NewIdentity(IWeb3Identity.Web3IdentitySource.Guest)));
            Assert.That(json.Remove("source"), Is.True, "the payload is expected to carry a source property");

            // Act
            IWeb3Identity? restored = serializer.Deserialize(json.ToString());

            // Assert
            Assert.That(restored!.Source, Is.EqualTo(IWeb3Identity.Web3IdentitySource.Cached));
        }

        [Test]
        public void FallBackToCachedWhenSourceIsUnrecognised()
        {
            // Arrange
            var json = JObject.Parse(serializer.Serialize(NewIdentity(IWeb3Identity.Web3IdentitySource.Guest)));
            json["source"] = "SomethingElse";

            // Act
            IWeb3Identity? restored = serializer.Deserialize(json.ToString());

            // Assert
            Assert.That(restored!.Source, Is.EqualTo(IWeb3Identity.Web3IdentitySource.Cached));
        }

        private IWeb3Identity NewIdentity(IWeb3Identity.Web3IdentitySource source)
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

            return new DecentralandIdentity(signer.Address, ephemeral, expiration, authChain, source);
        }
    }
}