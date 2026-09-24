using DCL.Web3.Accounts.Factory;
using DCL.Web3.Authenticators;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Nethereum.Signer;
using NUnit.Framework;
using System.Threading;

namespace DCL.Web3.Tests
{
    [TestFixture]
    public class EphemeralWeb3AuthenticatorShould
    {
        private static readonly EthereumMessageSigner MESSAGE_SIGNER = new ();

        private EphemeralWeb3Authenticator authenticator = null!;

        [SetUp]
        public void SetUp()
        {
            authenticator = new EphemeralWeb3Authenticator(new Web3AccountFactory());
        }

        [TearDown]
        public void TearDown()
        {
            authenticator.Dispose();
        }

        [Test]
        public void ResolveAnEphemeralGuestIdentity()
        {
            // Act
            IWeb3Identity identity = Login();

            // Assert
            Assert.That(identity.Method, Is.EqualTo(LoginMethod.EPHEMERAL_GUEST));
            Assert.That(identity.IsExpired, Is.False);
        }

        [Test]
        public void SignTheEphemeralMessageWithTheSignerOfTheChain()
        {
            // Act
            IWeb3Identity identity = Login();

            // Assert
            AuthLink ephemeral = identity.AuthChain.Get(AuthLinkType.ECDSA_EPHEMERAL);
            string recovered = MESSAGE_SIGNER.EncodeUTF8AndEcRecover(ephemeral.payload, ephemeral.signature);

            Assert.That(recovered, Is.EqualTo(identity.AuthChain.Get(AuthLinkType.SIGNER).payload).IgnoreCase);
            Assert.That(recovered, Is.EqualTo(identity.Address.ToString()).IgnoreCase);
        }

        [Test]
        public void NameTheEphemeralAccountInTheSignedMessage()
        {
            // Act
            IWeb3Identity identity = Login();

            // Assert
            Assert.That(identity.AuthChain.Get(AuthLinkType.ECDSA_EPHEMERAL).payload,
                Does.Contain(identity.EphemeralAccount.Address.OriginalFormat).IgnoreCase);
        }

        [Test]
        public void SignEntitiesWithTheEphemeralAccount()
        {
            // Arrange
            IWeb3Identity identity = Login();
            const string ENTITY_ID = "bafkreieph3em3al6ptwuiz5b4bwwq5iuxsxvmpbqcxbdfqn3p47a3lvxpi";

            // Act
            using AuthChain signed = identity.Sign(ENTITY_ID);

            // Assert
            AuthLink entity = signed.Get(AuthLinkType.ECDSA_SIGNED_ENTITY);
            string recovered = MESSAGE_SIGNER.EncodeUTF8AndEcRecover(entity.payload, entity.signature);

            Assert.That(recovered, Is.EqualTo(identity.EphemeralAccount.Address.ToString()).IgnoreCase);
        }

        [Test]
        public void ReuseTheSameAccountForTheWholeSession()
        {
            // Act
            IWeb3Identity first = Login();
            IWeb3Identity second = Login();

            // Assert
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Address, Is.EqualTo(first.Address));
        }

        [Test]
        public void ResolveADifferentAccountForANewSession()
        {
            // Arrange
            IWeb3Identity first = Login();

            // Act
            using var otherSession = new EphemeralWeb3Authenticator(new Web3AccountFactory());
            IWeb3Identity second = otherSession.LoginAsync(LoginPayload.ForGuestFlow(), CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.That(second.Address, Is.Not.EqualTo(first.Address));
        }

        private IWeb3Identity Login() =>
            authenticator.LoginAsync(LoginPayload.ForGuestFlow(), CancellationToken.None).GetAwaiter().GetResult();
    }
}
